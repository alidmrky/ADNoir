import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

// Offline generation uses committed, genuine Development OpenAPI snapshots.
// --refresh downloads all three service documents before generation.
// Backend limitations: Notification endpoints declare no response schemas, and
// OpenAPI does not see the custom ToHttpResult response enum/null serializer.
// Until those metadata gaps are fixed, Notification records and enum members are
// parsed from the actual C# declarations, never fabricated OpenAPI schemas.
// Request binding uses ASP.NET's default numeric enum serializer; response enums
// use AdNoirJsonOptions' camelCase strings. Separate input types preserve this.
const ui = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const root = path.resolve(ui, '..');
const check = process.argv.includes('--check');
const services = { identity: 5080, sample: 5090, notification: 5100 };
const read = (file) => readFile(path.join(root, file), 'utf8');
const camel = (value) => value[0].toLowerCase() + value.slice(1);
const enums = new Map();
for (const [name, file] of Object.entries({
  DataScope: 'src/Shared/AdNoir.Domain.Abstractions/Repositories/DataScope.cs',
  TenantStatus: 'src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Domain/Tenants/Tenant.cs',
  EmailDeliveryStatus:
    'src/Services/Notification/AdNoir.Notification.Domain/Emails/EmailDeliveryStatus.cs',
})) {
  const source = (await read(file)).replace(/\/\/[^\r\n]*/g, '');
  const body = source.match(new RegExp(`enum ${name}\\s*\\{([^}]+)\\}`))?.[1];
  if (!body) throw new Error(`Cannot parse enum ${name}`);
  let next = 0;
  enums.set(
    name,
    body
      .split(',')
      .map((entry) => entry.trim())
      .filter(Boolean)
      .map((entry) => {
        const match = /^(\w+)(?:\s*=\s*(\d+))?$/.exec(entry);
        if (!match) throw new Error(`Unsupported enum declaration ${entry}`);
        const value = match[2] ? Number(match[2]) : next;
        next = value + 1;
        return { name: camel(match[1]), value };
      }),
  );
}

await mkdir(path.join(ui, 'src/contracts/generated'), { recursive: true });
for (const [service, port] of Object.entries(services)) {
  const snapshot = path.join(ui, 'openapi', `${service}.json`);
  if (process.argv.includes('--refresh')) {
    const response = await fetch(`http://localhost:${port}/openapi/v1.json`);
    if (!response.ok) throw new Error(`OpenAPI download failed for ${service}`);
    await writeFile(snapshot, `${JSON.stringify(await response.json(), null, 2)}\n`);
  }
  const document = JSON.parse(await readFile(snapshot, 'utf8'));
  const schemas = document.components?.schemas ?? {};
  const declarations = new Map();
  const canonical = (name) => name.replace(/(Dto)\d+$/, '$1');
  const emitEnum = (name, input) => {
    const exported = name + (input ? 'Input' : '');
    declarations.set(
      exported,
      `export type ${exported} = ${enums
        .get(name)
        .map((member) => (input ? member.value : JSON.stringify(member.name)))
        .join(' | ')};`,
    );
    return exported;
  };
  function type(schema, input = false) {
    if (schema.$ref) {
      const raw = schema.$ref.split('/').at(-1);
      const name = canonical(raw);
      if (enums.has(name)) return emitEnum(name, input);
      const exported = name + (input && name.endsWith('Dto') ? 'Input' : '');
      emit(exported, schemas[raw], input);
      return exported;
    }
    if (schema.enum) return schema.enum.map(JSON.stringify).join(' | ');
    if (schema.type === 'string') return 'string';
    if (['integer', 'number'].includes(schema.type)) return 'number';
    if (schema.type === 'boolean') return 'boolean';
    if (schema.type === 'array') return `Array<${type(schema.items, input)}>`;
    if (schema.additionalProperties)
      return `Record<string, ${type(schema.additionalProperties, input)}>`;
    throw new Error(`Unsupported payload schema: ${JSON.stringify(schema)}`);
  }
  function emit(name, schema, input = false) {
    if (declarations.has(name)) return;
    if (!schema?.properties) throw new Error(`Missing payload properties for ${name}`);
    declarations.set(name, '');
    const fields = Object.entries(schema.properties).map(([key, value]) => {
      const optional = value.nullable || !schema.required?.includes(key);
      return `  ${key}${optional ? '?' : ''}: ${type(value, input)};`;
    });
    declarations.set(name, `export interface ${name} {\n${fields.join('\n')}\n}`);
  }
  for (const [name, schema] of Object.entries(schemas)) {
    if (/^(BaseResponse|PagedResponse|Response|MessageSeverity)/.test(name)) continue;
    if (/(Dto|Request)$/.test(name)) emit(name, schema, name.endsWith('Request'));
  }
  if (service === 'notification' && !schemas.EmailDeliveryDto) {
    const directory = 'src/Services/Notification/AdNoir.Notification.Application/Emails/Dtos';
    const csType = (raw) => {
      const value = raw.replace(/\?$/, '');
      const list = /^IReadOnlyList<(.+)>$/.exec(value);
      if (list) return `Array<${csType(list[1])}>`;
      const dictionary = /^IReadOnlyDictionary<string,\s*(.+)>$/.exec(value);
      if (dictionary) return `Record<string, ${csType(dictionary[1])}>`;
      if (enums.has(value)) return emitEnum(value, false);
      if (['string', 'Guid', 'DateTime'].includes(value)) return 'string';
      if (['int', 'long', 'double'].includes(value)) return 'number';
      if (value === 'bool') return 'boolean';
      if (value === 'EmailAttemptDto') return value;
      throw new Error(`Unsupported C# payload type ${value}`);
    };
    for (const name of ['EmailAttemptDto', 'EmailDeliveryDto', 'EmailStatisticsDto']) {
      const source = await read(`${directory}/${name}.cs`);
      const body = source.match(new RegExp(`record ${name}\\(([^;]+)\\);`))?.[1];
      if (!body) throw new Error(`Cannot parse positional record ${name}`);
      const fields = body.split(/,(?![^<]*>)/).map((field) => {
        const match = /^(.+)\s+(\w+)$/.exec(field.trim());
        if (!match) throw new Error(`Cannot parse record field ${field}`);
        return `  ${camel(match[2])}${match[1].endsWith('?') ? '?' : ''}: ${csType(match[1])};`;
      });
      declarations.set(name, `export interface ${name} {\n${fields.join('\n')}\n}`);
    }
  }
  const output = `// Generated by scripts/generate-contracts.mjs. Do not edit.\n// Source: openapi/${service}.json; see generator for documented backend metadata corrections.\n\n${[
    ...declarations,
  ]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([, value]) => value)
    .join('\n\n')}\n`;
  const destination = path.join(ui, 'src/contracts/generated', `${service}.ts`);
  if (check) {
    if ((await readFile(destination, 'utf8')) !== output)
      throw new Error(`Generated contract drift: ${service}`);
  } else await writeFile(destination, output);
  console.log(
    `${service}: ${declarations.size} payload declarations ${check ? 'verified' : 'generated'}`,
  );
}
