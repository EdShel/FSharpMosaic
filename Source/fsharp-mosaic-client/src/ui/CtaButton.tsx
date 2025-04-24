import React from "react";
import clsx from "clsx";
import styles from "./CtaButton.module.css";

type Props = React.PropsWithChildren<{
  onClick?(): void;
  className?: string;
  type?: "submit" | "reset" | "button";
  disabled?: boolean;
}>;

const CtaButton: React.FC<Props> = ({ className, ...otherProps }) => {
  return <button className={clsx(styles.button, className)} {...otherProps} />;
};

export default CtaButton;
