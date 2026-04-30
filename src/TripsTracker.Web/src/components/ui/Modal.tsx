import styles from './Modal.module.scss';

interface Props {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
  width?: number;
}

export default function Modal({ title, onClose, children, width = 480 }: Props) {
  return (
    <div className={styles.overlay}>
      <div className={styles.modal} style={{ width }}>
        <div className={styles.header}>
          <h3>{title}</h3>
          <button className={styles.close} type="button" onClick={onClose}>×</button>
        </div>
        <div className={styles.body}>{children}</div>
      </div>
    </div>
  );
}
