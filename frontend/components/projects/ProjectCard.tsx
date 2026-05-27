"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Avatar } from "@/components/ui/Avatar";
import { Icon } from "@/components/ui/Icon";

type ProjectSummary = {
  id: string;
  slug: string;
  name: string;
  ownerId: string;
  ownerName: string;
  createdAt: string;
};

interface ProjectCardProps {
  project: ProjectSummary;
  me: { id: string } | null;
  onEdit?: () => void;
  onDelete?: () => void;
}

export function ProjectCard({ project, me, onEdit, onDelete }: ProjectCardProps) {
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const isOwner = me !== null && project.ownerId === me.id;

  // Close dropdown when clicking outside
  useEffect(() => {
    if (!menuOpen) return;

    function handleOutsideClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    }

    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, [menuOpen]);

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={() => router.push(`/projects/${project.slug}`)}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          router.push(`/projects/${project.slug}`);
        }
      }}
      style={{
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius)",
        boxShadow: "var(--shadow-sm)",
        padding: "16px",
        display: "flex",
        flexDirection: "column",
        gap: "10px",
        transition: "box-shadow 0.15s ease",
        position: "relative",
        cursor: "pointer",
      }}
      onMouseEnter={(e) => {
        (e.currentTarget as HTMLDivElement).style.boxShadow = "var(--shadow-md)";
      }}
      onMouseLeave={(e) => {
        (e.currentTarget as HTMLDivElement).style.boxShadow = "var(--shadow-sm)";
      }}
    >
      {/* Header row: name + dots menu */}
      <div
        style={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
          gap: "8px",
        }}
      >
        {/* Project name + slug chip */}
        <div style={{ display: "flex", flexDirection: "column", gap: "4px", minWidth: 0 }}>
          <span
            style={{
              fontFamily: "'Manrope', sans-serif",
              fontWeight: 600,
              fontSize: 15,
              color: "var(--ink-0)",
              lineHeight: 1.3,
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
            }}
          >
            {project.name}
          </span>

          <code
            style={{
              background: "var(--surface-3)",
              borderRadius: "var(--radius-sm)",
              fontFamily: "monospace",
              fontSize: 11,
              padding: "2px 6px",
              color: "var(--ink-2)",
              alignSelf: "flex-start",
            }}
          >
            /{project.slug}
          </code>
        </div>

        {/* Dots menu — only shown to project owner */}
        {isOwner && (
          <div ref={menuRef} style={{ position: "relative", flexShrink: 0 }}>
            <button
              aria-label="Project options"
              aria-expanded={menuOpen}
              onClick={(e) => { e.stopPropagation(); setMenuOpen((prev) => !prev); }}
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                width: 28,
                height: 28,
                borderRadius: "var(--radius-sm)",
                border: "none",
                background: menuOpen ? "var(--surface-2)" : "transparent",
                color: "var(--ink-2)",
                cursor: "pointer",
                padding: 0,
                transition: "background 0.1s",
              }}
              onMouseEnter={(e) => {
                if (!menuOpen) {
                  e.currentTarget.style.background = "var(--surface-2)";
                }
              }}
              onMouseLeave={(e) => {
                if (!menuOpen) {
                  e.currentTarget.style.background = "transparent";
                }
              }}
            >
              <Icon name="dots" size={16} color="var(--ink-2)" />
            </button>

            {menuOpen && (
              <div
                style={{
                  position: "absolute",
                  top: "calc(100% + 4px)",
                  right: 0,
                  background: "var(--surface)",
                  border: "1px solid var(--border)",
                  borderRadius: "var(--radius-sm)",
                  boxShadow: "var(--shadow-md)",
                  minWidth: 120,
                  zIndex: 10,
                  overflow: "hidden",
                }}
              >
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    setMenuOpen(false);
                    onEdit?.();
                  }}
                  style={{
                    display: "block",
                    width: "100%",
                    textAlign: "left",
                    padding: "8px 12px",
                    fontSize: 13,
                    color: "var(--ink-1)",
                    background: "transparent",
                    border: "none",
                    cursor: "pointer",
                    transition: "background 0.1s",
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = "var(--surface-2)";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = "transparent";
                  }}
                >
                  Edit
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    setMenuOpen(false);
                    onDelete?.();
                  }}
                  style={{
                    display: "block",
                    width: "100%",
                    textAlign: "left",
                    padding: "8px 12px",
                    fontSize: 13,
                    color: "var(--ink-1)",
                    background: "transparent",
                    border: "none",
                    cursor: "pointer",
                    transition: "background 0.1s",
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = "var(--surface-2)";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = "transparent";
                  }}
                >
                  Delete
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Bottom row: owner avatar + name */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: "6px",
          marginTop: "auto",
        }}
      >
        <Avatar id={project.ownerId} name={project.ownerName} size={20} />
        <span
          style={{
            fontSize: 12,
            color: "var(--ink-2)",
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {project.ownerName}
        </span>
      </div>
    </div>
  );
}
