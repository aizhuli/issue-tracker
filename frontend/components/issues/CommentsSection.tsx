"use client";

import { useState, useEffect, useRef, KeyboardEvent } from "react";
import { Avatar } from "@/components/ui/Avatar";
import type { CommentDto } from "@/lib/types/issues";

function formatRelative(dateStr: string): string {
  const diffMs = new Date(dateStr).getTime() - Date.now();
  const diffSec = Math.round(diffMs / 1000);
  const absSec = Math.abs(diffSec);

  const rtf = new Intl.RelativeTimeFormat("en", { numeric: "auto" });

  if (absSec < 60) return rtf.format(Math.sign(diffSec) * absSec, "second");
  const diffMin = Math.round(diffSec / 60);
  if (Math.abs(diffMin) < 60) return rtf.format(diffMin, "minute");
  const diffHr = Math.round(diffSec / 3600);
  if (Math.abs(diffHr) < 24) return rtf.format(diffHr, "hour");
  const diffDay = Math.round(diffSec / 86400);
  if (Math.abs(diffDay) < 30) return rtf.format(diffDay, "day");
  const diffMonth = Math.round(diffSec / (86400 * 30));
  if (Math.abs(diffMonth) < 12) return rtf.format(diffMonth, "month");
  const diffYear = Math.round(diffSec / (86400 * 365));
  return rtf.format(diffYear, "year");
}

type MenuState = "edit" | "delete" | null;

interface CommentItemProps {
  comment: CommentDto;
  isOwn: boolean;
  projectSlug: string;
  issueNumber: number;
  onUpdated: (updated: CommentDto) => void;
  onDeleted: (id: string) => void;
}

function CommentItem({
  comment,
  isOwn,
  projectSlug,
  issueNumber,
  onUpdated,
  onDeleted,
}: CommentItemProps) {
  const [menu, setMenu] = useState<MenuState>(null);
  const [editBody, setEditBody] = useState(comment.body);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (menu !== null) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setEditBody(comment.body);
  }, [menu, comment.body]);

  useEffect(() => {
    function handleMouseDown(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        if (menu === null) return;
        setMenu(null);
      }
    }
    document.addEventListener("mousedown", handleMouseDown);
    return () => document.removeEventListener("mousedown", handleMouseDown);
  }, [menu]);

  async function handleSaveEdit() {
    if (!editBody.trim() || saving) return;
    setSaving(true);
    try {
      const res = await fetch(
        `/api/projects/${projectSlug}/issues/${issueNumber}/comments/${comment.id}`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ body: editBody }),
        }
      );
      if (!res.ok) throw new Error("Failed to update comment");
      const updated: CommentDto = await res.json();
      onUpdated(updated);
      setMenu(null);
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (deleting) return;
    setDeleting(true);
    try {
      const res = await fetch(
        `/api/projects/${projectSlug}/issues/${issueNumber}/comments/${comment.id}`,
        { method: "DELETE" }
      );
      if (!res.ok) throw new Error("Failed to delete comment");
      onDeleted(comment.id);
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div className="comment">
      <Avatar id={comment.authorId} name={comment.authorName} size={28} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div className="comment__meta">
          <span className="comment__author">{comment.authorName}</span>
          <span className="comment__time">{formatRelative(comment.createdAt)}</span>
          {comment.edited && <span className="comment__edited">(edited)</span>}
          {isOwn && (
            <div ref={menuRef} style={{ marginLeft: "auto", position: "relative" }}>
              <button
                aria-label="Comment actions"
                style={{
                  background: "none",
                  border: "none",
                  cursor: "pointer",
                  color: "var(--ink-3)",
                  fontSize: 14,
                  padding: "0 4px",
                  lineHeight: 1,
                }}
                onClick={() => setMenu((m) => (m === null ? "edit" : null))}
              >
                •••
              </button>
              {menu === null ? null : menu === "edit" ? (
                <div
                  style={{
                    position: "absolute",
                    top: "100%",
                    right: 0,
                    zIndex: 20,
                    background: "var(--surface-2, #1e1e2e)",
                    border: "1px solid var(--border, #2e2e3e)",
                    borderRadius: 6,
                    minWidth: 120,
                    boxShadow: "0 4px 16px rgba(0,0,0,0.25)",
                    overflow: "hidden",
                  }}
                >
                  <button
                    style={{
                      display: "block",
                      width: "100%",
                      padding: "8px 14px",
                      background: "transparent",
                      border: "none",
                      cursor: "pointer",
                      color: "var(--text-primary, #e0e0ef)",
                      fontSize: 13,
                      textAlign: "left",
                    }}
                    onClick={() => setMenu("edit")}
                  >
                    Edit
                  </button>
                  <button
                    style={{
                      display: "block",
                      width: "100%",
                      padding: "8px 14px",
                      background: "transparent",
                      border: "none",
                      cursor: "pointer",
                      color: "var(--red, #e05c5c)",
                      fontSize: 13,
                      textAlign: "left",
                    }}
                    onClick={() => setMenu("delete")}
                  >
                    Delete
                  </button>
                </div>
              ) : null}
            </div>
          )}
        </div>

        {menu === "edit" ? (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            <textarea
              className="comment-composer__input"
              value={editBody}
              onChange={(e) => setEditBody(e.target.value)}
              style={{ minHeight: 60 }}
              autoFocus
            />
            <div style={{ display: "flex", gap: 8 }}>
              <button
                className="btn btn--primary"
                disabled={!editBody.trim() || saving}
                onClick={handleSaveEdit}
              >
                {saving ? "Saving…" : "Save"}
              </button>
              <button className="btn btn--ghost" onClick={() => setMenu(null)}>
                Cancel
              </button>
            </div>
          </div>
        ) : menu === "delete" ? (
          <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "6px 0" }}>
            <span style={{ fontSize: 13, color: "var(--ink-1)" }}>Delete this comment?</span>
            <button
              className="btn btn--danger"
              disabled={deleting}
              onClick={handleDelete}
            >
              {deleting ? "Deleting…" : "Yes"}
            </button>
            <button className="btn btn--ghost" onClick={() => setMenu(null)}>
              No
            </button>
          </div>
        ) : (
          <div
            className="comment__body"
            style={{ whiteSpace: "pre-wrap", fontSize: 13.5, color: "var(--ink-1)" }}
          >
            {comment.body}
          </div>
        )}
      </div>
    </div>
  );
}

interface CommentsSectionProps {
  projectSlug: string;
  issueNumber: number;
  me: { id: string; name: string; avatarUrl?: string | null };
}

export function CommentsSection({ projectSlug, issueNumber, me }: CommentsSectionProps) {
  const [comments, setComments] = useState<CommentDto[]>([]);
  const [nextPageToken, setNextPageToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [composerBody, setComposerBody] = useState("");
  const [posting, setPosting] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  async function fetchComments(pageToken?: string) {
    const params = new URLSearchParams({ maxPageSize: "20" });
    if (pageToken) params.set("pageToken", pageToken);
    const res = await fetch(
      `/api/projects/${projectSlug}/issues/${issueNumber}/comments?${params}`
    );
    if (!res.ok) throw new Error("Failed to load comments");
    return res.json() as Promise<{ items: CommentDto[]; nextPageToken: string | null }>;
  }

  useEffect(() => {
    let cancelled = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLoading(true);
    fetchComments()
      .then((data) => {
        if (cancelled) return;
        setComments(data.items);
        setNextPageToken(data.nextPageToken ?? null);
      })
      .catch(() => {})
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [projectSlug, issueNumber]);

  async function handleLoadMore() {
    if (!nextPageToken || loadingMore) return;
    setLoadingMore(true);
    try {
      const data = await fetchComments(nextPageToken);
      setComments((prev) => [...prev, ...data.items]);
      setNextPageToken(data.nextPageToken ?? null);
    } catch {
    } finally {
      setLoadingMore(false);
    }
  }

  async function handlePost() {
    if (!composerBody.trim() || posting) return;
    setPosting(true);
    try {
      const res = await fetch(
        `/api/projects/${projectSlug}/issues/${issueNumber}/comments`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ body: composerBody }),
        }
      );
      if (!res.ok) throw new Error("Failed to post comment");
      const created: CommentDto = await res.json();
      setComments((prev) => [...prev, created]);
      setComposerBody("");
    } catch {
    } finally {
      setPosting(false);
    }
  }

  function handleKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === "Enter" && (e.metaKey || e.ctrlKey)) {
      e.preventDefault();
      handlePost();
    }
  }

  function handleCommentUpdated(updated: CommentDto) {
    setComments((prev) => prev.map((c) => (c.id === updated.id ? updated : c)));
  }

  function handleCommentDeleted(id: string) {
    setComments((prev) => prev.filter((c) => c.id !== id));
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
      {loading ? (
        <p style={{ fontSize: 13, color: "var(--ink-3)", padding: "8px 0" }}>Loading comments…</p>
      ) : comments.length === 0 ? (
        <p style={{ fontSize: 13, color: "var(--ink-3)", padding: "8px 0" }}>No comments yet.</p>
      ) : (
        <div style={{ display: "flex", flexDirection: "column" }}>
          {comments.map((comment) => (
            <CommentItem
              key={comment.id}
              comment={comment}
              isOwn={comment.authorId === me.id}
              projectSlug={projectSlug}
              issueNumber={issueNumber}
              onUpdated={handleCommentUpdated}
              onDeleted={handleCommentDeleted}
            />
          ))}
        </div>
      )}

      {nextPageToken && (
        <div style={{ padding: "10px 0" }}>
          <button
            className="btn btn--ghost"
            onClick={handleLoadMore}
            disabled={loadingMore}
            style={{ fontSize: 13 }}
          >
            {loadingMore ? "Loading…" : "Load more"}
          </button>
        </div>
      )}

      <div className="comment-composer">
        <div style={{ display: "flex", gap: 10, alignItems: "flex-start" }}>
          <Avatar id={me.id} name={me.name} size={28} />
          <textarea
            ref={textareaRef}
            className="comment-composer__input"
            placeholder="Leave a comment… (Cmd+Enter to post)"
            value={composerBody}
            onChange={(e) => setComposerBody(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={posting}
          />
        </div>
        <div className="comment-composer__footer">
          <button
            className="btn btn--primary"
            disabled={!composerBody.trim() || posting}
            onClick={handlePost}
          >
            {posting ? "Posting…" : "Post"}
          </button>
        </div>
      </div>
    </div>
  );
}
