export interface Claims {
  sub?: string;
  email?: string;
  name?: string;
  tenant_id?: string;
  roles?: string | string[];
  read_scope?: string;
  write_scope?: string;
  hierarchy_path?: string;
}
