import React from "react";
import clsx from "clsx";
import { SseEvent } from "./types";
import styles from "./LogEventBlock.module.css";

interface Props {
  event: SseEvent;
}

const LogEventBlock: React.FC<Props> = ({ event }) => {
  switch (event.type) {
    case "error": {
      return (
        <div className={clsx(styles.container, styles.error)}>
          Error: {event.data}
        </div>
      );
    }
    case "validation": {
      return (
        <div className={clsx(styles.container, styles.error)}>
          {Object.entries(event.data.errors).flatMap(([field, errors]) =>
            errors.map((er) => `Validation error for "${field}": ${er}`)
          )}
        </div>
      );
    }
    case "progress": {
      return (
        <div className={clsx(styles.container, styles.progress)}>
          Progress: {((100 * event.data.current) / event.data.total).toFixed(2)}
          %
          <div
            style={{
              background: event.data.color,
              width: "1rem",
              height: "1rem",
            }}
          />
        </div>
      );
    }
    case "completed": {
      return <div className={styles.container}>Done!</div>;
    }
  }
};

export default LogEventBlock;
