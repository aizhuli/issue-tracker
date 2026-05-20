"use client";

import { FormEvent, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { FormField } from "@/components/ui/FormField";
import { PasswordInput } from "@/components/ui/PasswordInput";
import { FormError } from "@/components/ui/FormError";
import { registerSchema } from "@/lib/schemas/auth";

type FieldErrors = { name?: string; email?: string; password?: string };

export function RegisterForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setFieldErrors({});
    setFormError("");

    const result = registerSchema.safeParse({ name, email, password });
    if (!result.success) {
      const errs: FieldErrors = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0] as keyof FieldErrors;
        if (!errs[field]) errs[field] = issue.message;
      }
      setFieldErrors(errs);
      return;
    }

    setLoading(true);
    try {
      const res = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, email, password }),
      });

      if (res.ok) {
        const returnTo = searchParams.get("returnTo");
        router.push(returnTo?.startsWith("/") ? returnTo : "/");
        return;
      }

      const data = await res.json().catch(() => ({}));
      const code: string = data?.errorCode ?? "";

      if (code === "auth:user:email:already_exists") {
        setFieldErrors({ email: "An account with this email already exists" });
      } else if (code.startsWith("auth:user:email")) {
        setFieldErrors({ email: "Enter a valid email address" });
      } else if (code.startsWith("auth:user:password")) {
        setFieldErrors({ password: "Password must be 8–128 characters" });
      } else if (code.startsWith("auth:user:name")) {
        setFieldErrors({ name: "Name is required (max 100 chars)" });
      } else if (res.status >= 500) {
        setFormError("Something went wrong — please try again");
      } else {
        setFormError(data?.title ?? "Something went wrong — please try again");
      }
    } catch {
      setFormError("Something went wrong — please try again");
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <FormField
        label="Name"
        error={fieldErrors.name}
        inputProps={{
          type: "text",
          name: "name",
          autoComplete: "name",
          value: name,
          onChange: (e) => setName(e.target.value),
          placeholder: "Your name",
        }}
      />
      <FormField
        label="Email"
        error={fieldErrors.email}
        inputProps={{
          type: "email",
          name: "email",
          autoComplete: "email",
          value: email,
          onChange: (e) => setEmail(e.target.value),
          placeholder: "you@example.com",
        }}
      />
      <PasswordInput
        label="Password"
        error={fieldErrors.password}
        inputProps={{
          name: "password",
          autoComplete: "new-password",
          value: password,
          onChange: (e) => setPassword(e.target.value),
          placeholder: "Min 8 characters",
        }}
      />
      <FormError message={formError} />
      <button
        type="submit"
        disabled={loading}
        style={{
          width: "100%",
          height: 40,
          background: loading ? "var(--surface-3)" : "var(--accent-1)",
          color: loading ? "var(--ink-3)" : "var(--accent-1-ink)",
          border: "none",
          borderRadius: 9,
          fontSize: 13.5,
          fontWeight: 700,
          cursor: loading ? "not-allowed" : "pointer",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          gap: 8,
          transition: "background 0.12s",
        }}
      >
        {loading ? <Spinner /> : "Create account"}
      </button>
    </form>
  );
}

function Spinner() {
  return (
    <span
      style={{
        width: 16,
        height: 16,
        border: "2.5px solid rgba(27,58,27,0.3)",
        borderTopColor: "var(--accent-1-ink)",
        borderRadius: "50%",
        display: "inline-block",
        animation: "spin 0.7s linear infinite",
      }}
    />
  );
}
