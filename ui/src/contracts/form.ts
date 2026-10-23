export interface FormField {
  name: string;
  label?: string;
  type?:
    'text' | 'email' | 'password' | 'uuid' | 'checkbox' | 'textarea' | 'select' | 'permissions';
  optional?: boolean;
  options?: { value: string; label: string }[];
}
