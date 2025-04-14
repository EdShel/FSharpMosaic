const backendUrl = process.env.NEXT_PUBLIC_API_BASE_URL;

if (!backendUrl) {
  throw new Error("Backend URL must be provided.");
}

export const postCreateMosaic = async (formData: FormData) => {
//   const formData = new FormData();
//   formData.append("SourceImage", sourceImage);
//   formData.append("ImagesX", String(imagesX));
//   formData.append("ImagesY", String(imagesY));

  const response = await fetch(`${backendUrl}/api/v1/mosaics`, {
    method: "POST",
    body: formData,
  });
  await ensureSuccessfulResponse(response);

  return await response.blob();
};

async function ensureSuccessfulResponse(response: Response) {
  if (!response.ok) {
    const body = await response.text();
    throw new Error(
      `The API responded with ${response.status} ${response.statusText}: "${body}"`
    );
  }
}
