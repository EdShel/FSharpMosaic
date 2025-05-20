const backendUrl = process.env.NEXT_PUBLIC_API_BASE_URL;

if (!backendUrl) {
  throw new Error("Backend URL must be provided.");
}

export const postCreateMosaic = async (formData: FormData) => {
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

export const createImportZipEventSource = (zipFilePath: string) => {
  const query = new URLSearchParams();
  query.append("ZipFilePath", zipFilePath);
  return new EventSource(`${backendUrl}/api/v1/mosaics/import?${query}`);
};
