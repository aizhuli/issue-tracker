import { NextRequest } from "next/server";
import { passthrough, serverFetch } from "@/lib/api-client";
import { getSession } from "@/lib/session";

export async function POST(request: NextRequest) {
  const body = await request.text();

  const upstream = await serverFetch("/api/auth/login", {
    method: "POST",
    body,
  });

  if (upstream.ok) {
    const data = (await upstream.clone().json()) as {
      id: string;
      email: string;
      name: string;
    };
    const session = await getSession();
    session.user = { userId: data.id, email: data.email, name: data.name };
    await session.save();
  }

  return passthrough(upstream);
}
