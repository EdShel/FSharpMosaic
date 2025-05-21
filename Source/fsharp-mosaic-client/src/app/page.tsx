/* eslint-disable @next/next/no-img-element */
"use client";

import React, { useState } from "react";
import styles from "./page.module.css";
import { postCreateMosaic } from "@/lib/mosaicApi";
import Skeleton from "@/ui/Skeleton";
import CtaButton from "@/ui/CtaButton";
import Link from "next/link";

type FormStatus =
  | { status: "idle"; data?: undefined }
  | { status: "loading"; data?: undefined }
  | { status: "error"; data: string };

export default function Home() {
  const [sourceImagePreview, setSourceImagePreview] = useState<string | null>(
    null
  );
  const [mosaicImagePreview, setMosaicImagePreview] = useState<string | null>(
    null
  );
  const [formState, setFormState] = useState<FormStatus>({ status: "idle" });

  return (
    <main className={styles.container}>
      <h1>Mosaic generator</h1>

      <form
        onSubmit={async (ev) => {
          ev.preventDefault();

          const formData = new FormData(ev.currentTarget);
          const uploadedImage = formData.get("SourceImage") as File | null;
          if (!uploadedImage || uploadedImage.size === 0) {
            setFormState({ status: "error", data: "Please upload an image" });
            return;
          }

          setFormState({ status: "loading" });
          try {
            const blob = await postCreateMosaic(formData);
            const preview = URL.createObjectURL(blob);
            setMosaicImagePreview(preview);
            setFormState({ status: "idle" });
          } catch {
            setFormState({
              status: "error",
              data: "Unexpected error during processing, try again later",
            });
          }
        }}
        className={styles.form}
      >
        <div className={styles.previews}>
          <label className={styles.preview}>
            {sourceImagePreview && (
              <img
                className={styles.image}
                src={sourceImagePreview}
                alt="Source image"
              />
            )}
            {!sourceImagePreview && (
              <span className={styles.instruction}>Click or drag an image</span>
            )}

            <input
              type="file"
              name="SourceImage"
              accept="image/png, image/jpeg"
              className={styles.fileInput}
              onChange={(e) => {
                if (sourceImagePreview) {
                  URL.revokeObjectURL(sourceImagePreview);
                  setSourceImagePreview(null);
                }
                if (mosaicImagePreview) {
                  URL.revokeObjectURL(mosaicImagePreview);
                  setMosaicImagePreview(null);
                }

                const files = e.target.files;
                if (!files || files.length < 1) {
                  return;
                }
                const file = files[0];
                const filePreview = URL.createObjectURL(file);

                setSourceImagePreview(filePreview);
              }}
            />
          </label>
          <div className={styles.preview}>
            {(() => {
              if (formState.status === "loading") {
                return <Skeleton width="100%" height="100%" />;
              }

              if (mosaicImagePreview) {
                return (
                  <img
                    className={styles.image}
                    src={mosaicImagePreview}
                    alt="Source image"
                  />
                );
              }

              return (
                <span className={styles.instruction}>
                  The result will be here
                </span>
              );
            })()}
          </div>
        </div>

        <div className={styles.inputRow}>
          <label htmlFor="density">Density -</label>
          <input
            id="density"
            name="density"
            type="number"
            defaultValue={32}
            min={4}
            max={256}
          />
          <span>pieces per largest image dimension</span>
        </div>

        <div className={styles.inputRow}>
          <label htmlFor="resultImageSize">Image size -</label>
          <select
            id="resultImageSize"
            name="resultImageSize"
            defaultValue="512"
          >
            {[256, 512, 768, 1024, 2048, 4096].map((v) => (
              <option key={v} value={v}>
                {v} pixels
              </option>
            ))}
          </select>
          <span>(higher sizes take more time to be encoded)</span>
        </div>

        <div className={styles.buttonContainer}>
          <CtaButton type="submit" disabled={formState.status === "loading"}>
            Create mosaic
          </CtaButton>
        </div>

        {formState.status === "error" && (
          <strong className={styles.error}>{formState.data}</strong>
        )}
      </form>

      <Link href="/import-zip" className={styles.importLink}>
        Import data (Demo)
      </Link>
    </main>
  );
}
