// api.js
export async function postJSON(url, body, options = {}) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
    credentials: "include", // remove if not needed
    body: JSON.stringify(body),
    ...options,
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => "");
    throw new Error(
      `POST ${url} failed: ${response.status} ${errorText}`
    );
  }

  // handle empty 204
  if (response.status === 204) return null;

  return response.json();
}
