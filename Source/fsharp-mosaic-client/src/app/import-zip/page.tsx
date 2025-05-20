"use client";

import React, { useEffect, useRef, useState } from "react";
import { createImportZipEventSource } from "@/lib/mosaicApi";
import CtaButton from "@/ui/CtaButton";
import styles from "./page.module.css";

const MAX_VISIBLE_LOG_EVENTS = 10;

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

  return (
    <main className={styles.container}>
      <form
        onSubmit={(ev) => {
          ev.preventDefault();
          setLogEvents([]);

          const formData = new FormData(ev.currentTarget);
          const filePath = formData.get("ZipFilePath");
          if (typeof filePath !== "string") {
            return;
          }
          let logIdCounter = 1;

          const eventSource = createImportZipEventSource(filePath);
          eventSource.addEventListener("error", (event) => {
            console.log("event", event);
            eventSource.close();
          });

          eventSource.addEventListener("validation", (event) => {
            const data = JSON.parse(event.data) as ValidationEventData;
            setLogEvents((old) => [
              ...old,
              { id: ++logIdCounter, type: "validation" as const, data },
            ]);
          });

          eventSource.addEventListener("progress", (event) => {
            const data = JSON.parse(event.data) as ProgressEventData;
            setLogEvents((old) =>
              [
                ...old,
                { id: ++logIdCounter, type: "progress" as const, data },
              ].slice(-MAX_VISIBLE_LOG_EVENTS)
            );
          });
        }}
      >
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
            id="zipFilePath"
            name="ZipFilePath"
            type="text"
            defaultValue="C:/Users/eduard.sheliemietiev/Desktop/archive.zip"
            required
          />
          <label htmlFor="zipFilePath" />
        </div>

        <CtaButton type="submit">Import ZIP</CtaButton>

        {logEvents.map((ev) => {
          if (ev.type === "validation") {
            return (
              <div key={ev.id}>
                Validation error:
                {Object.entries(ev.data.errors).flatMap(([field, errors]) =>
                  errors.map((er) => `Validation error for "${field}": ${er}`)
                )}
              </div>
            );
          }
          if (ev.type === "progress") {
            return (
              <div key={ev.id}>
                Progress: {((100 * ev.data.current) / ev.data.total).toFixed(2)}
                %
                <div
                  style={{
                    background: ev.data.color,
                    width: "1rem",
                    height: "1rem",
                  }}
                />
              </div>
            );
          }
        })}
      </form>
    </main>
  );
};

export default Page;

type ValidationEventData = {
  errors: Record<string, string[]>;
};

type ProgressEventData = {
  current: number;
  total: number;
  color: string;
};

type EventLog = {
  id: number;
} & (
  | {
      type: "validation";
      data: ValidationEventData;
    }
  | {
      type: "progress";
      data: ProgressEventData;
    }
);
