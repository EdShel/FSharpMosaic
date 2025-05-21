"use client";

import React, { useEffect, useRef, useState } from "react";
import { createImportZipEventSource } from "@/lib/mosaicApi";
import CtaButton from "@/ui/CtaButton";
import {
  CompletedEventData,
  ProgressEventData,
  SseEvent,
  ValidationEventData,
} from "./types";
import styles from "./page.module.css";
import LogEventBlock from "./LogEventBlock";

const MAX_VISIBLE_LOG_EVENTS = 10;

type EventLog = {
  id: number;
} & SseEvent;

const Page: React.FC<object> = ({}) => {
  const [logEvents, setLogEvents] = useState<EventLog[]>([]);
  const eventSourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    return () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }
    };
  }, []);

  const handleSubmit: React.FormEventHandler<HTMLFormElement> = (ev) => {
    ev.preventDefault();
    setLogEvents([]);

    const formData = new FormData(ev.currentTarget);
    const filePath = formData.get("ZipFilePath");
    if (typeof filePath !== "string") {
      return;
    }

    let logIdCounter = 1;
    const logEvent = (log: SseEvent) => {
      setLogEvents((old) =>
        [...old, { ...log, id: ++logIdCounter }].slice(-MAX_VISIBLE_LOG_EVENTS)
      );
    };

    const eventSource = createImportZipEventSource(filePath);
    eventSource.addEventListener("error", () => {
      logEvent({ type: "error", data: "Unexpected error has occurred." });
      eventSource.close();
    });

    eventSource.addEventListener("validation", (event) => {
      const data = JSON.parse(event.data) as ValidationEventData;
      logEvent({ type: "validation", data });
      eventSource.close();
    });
    eventSource.addEventListener("progress", (event) => {
      const data = JSON.parse(event.data) as ProgressEventData;
      logEvent({ type: "progress", data });
    });
    eventSource.addEventListener("completed", (event) => {
      const data = JSON.parse(event.data) as CompletedEventData;
      logEvent({ type: "completed", data });
      eventSource.close();
    });
  };

  return (
    <main className={styles.container}>
      <form onSubmit={handleSubmit}>
        <div>
          {
            "This demo page allows extracting source images for mosaics from a ZIP archive."
          }
          {
            "The archive must be available to the server's file system. It must contain .jpg/.png images (subfolders are allowed)."
          }
        </div>

        <div>
          <input
            className={styles.input}
            id="zipFilePath"
            name="ZipFilePath"
            type="text"
            defaultValue="C:/Users/eduard.sheliemietiev/Desktop/archive.zip"
            required
          />
          <label htmlFor="zipFilePath" />
        </div>

        <div className={styles.buttonWrapper}>
          <CtaButton type="submit">Import ZIP</CtaButton>
        </div>

        <div className={styles.logs}>
          {logEvents.map((ev) => (
            <LogEventBlock key={ev.id} event={ev} />
          ))}
        </div>
      </form>
    </main>
  );
};

export default Page;
