"use client";

const SWATCHES = ["#7B95B8", "#9E7BC1", "#D4A24C", "#6FAE5A", "#B6DF7B", "#A8B0A2"];

function hashId(id: string): number {
  let sum = 0;
  for (let i = 0; i < id.length; i++) {
    sum += id.charCodeAt(i);
  }
  return sum % SWATCHES.length;
}

interface AvatarProps {
  id: string;
  name?: string;
  size?: number;
}

export function Avatar({ id, name, size = 24 }: AvatarProps) {
  const bg = SWATCHES[hashId(id)];
  const initial = name ? name.charAt(0).toUpperCase() : "?";
  const fontSize = Math.round(size * 0.4);

  return (
    <div
      aria-label={name ?? "Unknown user"}
      style={{
        width: size,
        height: size,
        borderRadius: "50%",
        background: bg,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        flexShrink: 0,
        color: "#ffffff",
        fontSize,
        fontWeight: 700,
        lineHeight: 1,
        userSelect: "none",
      }}
    >
      {initial}
    </div>
  );
}
