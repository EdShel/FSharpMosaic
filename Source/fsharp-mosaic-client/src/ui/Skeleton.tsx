import React from "react";
import styles from "./Skeleton.module.css";

interface Props {
  width?: string | number;
  height?: string | number;
  style?: React.CSSProperties;
}

const Skeleton: React.FC<Props> = ({ width, height, style }) => {
  return (
    <div
      aria-hidden="true"
      className={styles.skeleton}
      style={{ width, height, ...style }}
    />
  );
};

export default Skeleton;
