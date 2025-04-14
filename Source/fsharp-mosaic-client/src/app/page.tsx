/* eslint-disable @next/next/no-img-element */
"use client";

import React, { useState } from "react";
import styles from "./page.module.css";
import { postCreateMosaic } from "@/lib/mosaicApi";

export default function Home() {
  const [sourceImagePreview, setSourceImagePreview] = useState<string | null>(
    null
  );
  const [mosaicImagePreview, setMosaicImagePreview] = useState<string | null>(
    null
  );

  // useEffect(() => {
  //   const abortController = new AbortController();

  //   return () => abortController.abort();
  // }, []);

  return (
    <div className={styles.container}>
      <h1>Mosaic generator</h1>
      
      <form
        onSubmit={async (data) => {
          data.preventDefault();

          const formData = new FormData(data.currentTarget);
          const blob = await postCreateMosaic(formData);
          const preview = URL.createObjectURL(blob);
          setMosaicImagePreview(preview);
        }}
        className={styles.form}
      >
        <div className={styles.previews}>
          {sourceImagePreview && (
            <div>
              <img
                className={styles.image}
                src={sourceImagePreview}
                alt="Source image"
              />
            </div>
          )}
          {mosaicImagePreview && (
            <div>
              <img
                className={styles.image}
                src={mosaicImagePreview}
                alt="Source image"
              />
            </div>
          )}
        </div>
        <input
          name="SourceImage"
          type="file"
          accept="image/png, image/jpeg"
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

        <input type="number" defaultValue={32} name="PieceSize" />

        <button type="submit">Create mosaic</button>
      </form>
    </div>
  );
}
