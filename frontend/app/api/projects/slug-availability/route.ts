import { NextRequest } from "next/server";
import { passthrough, serverFetch } from "@/lib/api-client";
import { getSession } from "@/lib/session";

const MISSING_SESSION_RESPONSE = {
  type: "https://tools.ietf.org/html/rfc7807",
  title: "Unauthorized",
  status: 401,
  detail: "No active session.",
  errorCode: "auth:session:missing",
};

export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session.user) {
    return Response.json(MISSING_SESSION_RESPONSE, {
      status: 401,
      headers: { "content-type": "application/problem+json" },
    });
  }

  const url = new URL(request.url);
  const pathWithQuery = `/api/projects/slug-availability${url.search}`;

  const upstream = await serverFetch(pathWithQuery, {
    method: "GET",
    user: session.user,
  });

  return passthrough(upstream);
}
