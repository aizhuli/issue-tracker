"use client";

interface FormErrorProps {
  message?: string;
}

export function FormError({ message }: FormErrorProps) {
  if (!message) return null;
  return (
    <div
      role="alert"
      style={{
        background: "#F0DDDD",
        color: "#7A3535",
        borderRadius: 8,
        padding: "8px 12px",
        fontSize: 12.5,
        lineHeight: 1.4,
      }}
    >
      {message}
    </div>
  );
}
