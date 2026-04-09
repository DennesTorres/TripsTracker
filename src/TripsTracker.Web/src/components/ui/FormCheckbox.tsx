import styles from './FormCheckbox.module.scss';

interface Props {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

export default function FormCheckbox({ label, checked, onChange }: Props) {
  return (
    <label className={styles.label}>
      <input
        type="checkbox"
        checked={checked}
        onChange={e => onChange(e.target.checked)}
      />
      {label}
    </label>
  );
}
