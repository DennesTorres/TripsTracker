import styles from './FormInput.module.scss';

interface Props {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  placeholder?: string;
  autoComplete?: string;
}

export default function FormInput({ label, value, onChange, required, placeholder, autoComplete }: Props) {
  return (
    <label className={styles.label}>
      {label}
      <input
        type="text"
        value={value}
        onChange={e => onChange(e.target.value)}
        required={required}
        placeholder={placeholder}
        autoComplete={autoComplete}
      />
    </label>
  );
}
