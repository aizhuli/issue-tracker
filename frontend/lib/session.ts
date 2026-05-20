import { cookies } from "next/headers";
import { getIronSession, SessionOptions } from "iron-session";

export type SessionUser = {
  userId: string;
  email: string;
  name: string;
};

export type SessionData = {
  user?: SessionUser;
};

function getSessionPassword(): string {
  const password = process.env.SESSION_COOKIE_PASSWORD;
  if (!password || password.length < 32) {
    throw new Error(
      "SESSION_COOKIE_PASSWORD must be set and be at least 32 characters long.",
    );
  }
  return password;
}

export function getSessionOptions(): SessionOptions {
  return {
    cookieName: "ait_session",
    password: getSessionPassword(),
    cookieOptions: {
      httpOnly: true,
      sameSite: "lax",
      secure: process.env.NODE_ENV === "production",
      path: "/",
    },
  };
}

export async function getSession() {
  const cookieStore = await cookies();
  return getIronSession<SessionData>(cookieStore, getSessionOptions());
}
