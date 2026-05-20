import type { SessionUser } from "./session";

const SECRET_HEADER = "X-Bff-Secret";
const USER_ID_HEADER = "X-User-Id";

function getApiUrl(): string {
  const url = process.env.API_URL ?? process.env.services__api__http__0;
  if (!url) {
    throw new Error("API_URL is not configured.");
  }
  return url.replace(/\/$/, "");
}

function getBffSecret(): string {
  const secret = process.env.BFF_SHARED_SECRET;
  if (!secret) {
    throw new Error("BFF_SHARED_SECRET is not configured.");
  }
  return secret;
}

export type ServerFetchOptions = RequestInit & {
  user?: SessionUser;
};

export async function serverFetch(
  path: string,
  options: ServerFetchOptions = {},
): Promise<Response> {
  const { user, headers, ...rest } = options;

  const finalHeaders = new Headers(headers);
  finalHeaders.set(SECRET_HEADER, getBffSecret());
  if (user) {
    finalHeaders.set(USER_ID_HEADER, user.userId);
  }
  if (!finalHeaders.has("Content-Type") && rest.body) {
    finalHeaders.set("Content-Type", "application/json");
  }

  const url = `${getApiUrl()}${path.startsWith("/") ? path : `/${path}`}`;
  return fetch(url, {
    ...rest,
    headers: finalHeaders,
    cache: "no-store",
  });
}

export async function passthrough(response: Response): Promise<Response> {
  const body = await response.text();
  const headers = new Headers();
  const contentType = response.headers.get("content-type");
  if (contentType) {
    headers.set("content-type", contentType);
  }
  return new Response(body || null, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}
