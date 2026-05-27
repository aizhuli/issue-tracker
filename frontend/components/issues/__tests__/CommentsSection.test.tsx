import { render, screen, fireEvent, act, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { CommentsSection } from "@/components/issues/CommentsSection";
import type { CommentDto } from "@/lib/types/issues";

const meUser = { id: "user-me", name: "Alice", avatarUrl: null };
const otherUser = { id: "user-other", name: "Bob", avatarUrl: null };

const ownComment: CommentDto = {
  id: "comment-1",
  authorId: "user-me",
  authorName: "Alice",
  authorAvatarUrl: null,
  body: "This is my comment",
  createdAt: new Date(Date.now() - 60000).toISOString(),
  updatedAt: new Date(Date.now() - 60000).toISOString(),
  edited: false,
};

const otherComment: CommentDto = {
  id: "comment-2",
  authorId: "user-other",
  authorName: "Bob",
  authorAvatarUrl: null,
  body: "This is Bob's comment",
  createdAt: new Date(Date.now() - 120000).toISOString(),
  updatedAt: new Date(Date.now() - 120000).toISOString(),
  edited: false,
};

function makeFetchComments(comments: CommentDto[], nextPageToken: string | null = null) {
  return vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve({ items: comments, nextPageToken }),
  });
}

function renderSection(
  comments: CommentDto[] = [],
  me = meUser,
  fetchMock?: ReturnType<typeof vi.fn>
) {
  vi.stubGlobal("fetch", fetchMock ?? makeFetchComments(comments));
  return render(
    <CommentsSection projectSlug="my-proj" issueNumber={1} me={me} />
  );
}

describe("CommentsSection", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("fetches comments on mount", async () => {
    const fetchMock = makeFetchComments([]);
    renderSection([], meUser, fetchMock);
    await act(async () => { await vi.runAllTimersAsync(); });
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/api/projects/my-proj/issues/1/comments")
    );
  });

  it("shows No comments yet when there are no comments", async () => {
    renderSection([]);
    await act(async () => { await vi.runAllTimersAsync(); });
    expect(screen.getByText(/no comments yet/i)).toBeInTheDocument();
  });

  it("renders comment bodies", async () => {
    renderSection([ownComment, otherComment]);
    await act(async () => { await vi.runAllTimersAsync(); });
    expect(screen.getByText("This is my comment")).toBeInTheDocument();
    expect(screen.getByText("This is Bob's comment")).toBeInTheDocument();
  });

  it("own comments show ••• action button", async () => {
    renderSection([ownComment]);
    await act(async () => { await vi.runAllTimersAsync(); });
    expect(screen.getByRole("button", { name: /comment actions/i })).toBeInTheDocument();
  });

  it("non-own comments do not show ••• action button", async () => {
    renderSection([otherComment]);
    await act(async () => { await vi.runAllTimersAsync(); });
    expect(screen.queryByRole("button", { name: /comment actions/i })).toBeNull();
  });

  it("both own and other comments rendered: only own has action button", async () => {
    renderSection([ownComment, otherComment]);
    await act(async () => { await vi.runAllTimersAsync(); });
    const actionButtons = screen.getAllByRole("button", { name: /comment actions/i });
    expect(actionButtons).toHaveLength(1);
  });

  it("Cmd+Enter in composer textarea posts comment", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ items: [], nextPageToken: null }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({
          ...ownComment,
          id: "comment-new",
          body: "New comment",
        }),
      });
    vi.stubGlobal("fetch", fetchMock);

    render(<CommentsSection projectSlug="my-proj" issueNumber={1} me={meUser} />);
    await act(async () => { await vi.runAllTimersAsync(); });

    const textarea = screen.getByPlaceholderText(/leave a comment/i);
    fireEvent.change(textarea, { target: { value: "New comment" } });
    fireEvent.keyDown(textarea, { key: "Enter", metaKey: true });

    await act(async () => { await vi.runAllTimersAsync(); });

    expect(fetchMock).toHaveBeenLastCalledWith(
      expect.stringContaining("/comments"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ body: "New comment" }),
      })
    );
  });

  it("Ctrl+Enter also posts comment", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ items: [], nextPageToken: null }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ ...ownComment, id: "comment-new", body: "Ctrl comment" }),
      });
    vi.stubGlobal("fetch", fetchMock);

    render(<CommentsSection projectSlug="my-proj" issueNumber={1} me={meUser} />);
    await act(async () => { await vi.runAllTimersAsync(); });

    const textarea = screen.getByPlaceholderText(/leave a comment/i);
    fireEvent.change(textarea, { target: { value: "Ctrl comment" } });
    fireEvent.keyDown(textarea, { key: "Enter", ctrlKey: true });

    await act(async () => { await vi.runAllTimersAsync(); });

    expect(fetchMock).toHaveBeenLastCalledWith(
      expect.stringContaining("/comments"),
      expect.objectContaining({ method: "POST" })
    );
  });

  it("plain Enter does not submit (inserts newline)", async () => {
    const fetchMock = makeFetchComments([]);
    vi.stubGlobal("fetch", fetchMock);

    render(<CommentsSection projectSlug="my-proj" issueNumber={1} me={meUser} />);
    await act(async () => { await vi.runAllTimersAsync(); });

    const textarea = screen.getByPlaceholderText(/leave a comment/i);
    fireEvent.change(textarea, { target: { value: "Hello" } });
    fireEvent.keyDown(textarea, { key: "Enter" });

    // Should only have the initial comments fetch, not a POST
    const postCalls = fetchMock.mock.calls.filter(
      (call) => typeof call[1] === "object" && (call[1] as RequestInit)?.method === "POST"
    );
    expect(postCalls).toHaveLength(0);
  });

  it("Post button submits the comment", async () => {
    const newComment = { ...ownComment, id: "comment-new", body: "Button post" };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ items: [], nextPageToken: null }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(newComment),
      });
    vi.stubGlobal("fetch", fetchMock);

    render(<CommentsSection projectSlug="my-proj" issueNumber={1} me={meUser} />);
    await act(async () => { await vi.runAllTimersAsync(); });

    const textarea = screen.getByPlaceholderText(/leave a comment/i);
    fireEvent.change(textarea, { target: { value: "Button post" } });
    fireEvent.click(screen.getByRole("button", { name: /^post$/i }));

    await act(async () => { await vi.runAllTimersAsync(); });

    expect(screen.getByText("Button post")).toBeInTheDocument();
  });

  it("Edit path: clicking ••• shows Edit/Delete menu items", async () => {
    renderSection([ownComment]);
    await act(async () => { await vi.runAllTimersAsync(); });

    const actionsBtn = screen.getByRole("button", { name: /comment actions/i });
    fireEvent.click(actionsBtn);

    expect(screen.getByRole("button", { name: /^edit$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^delete$/i })).toBeInTheDocument();
  });

  it("Delete path: clicking Delete shows confirm prompt", async () => {
    renderSection([ownComment]);
    await act(async () => { await vi.runAllTimersAsync(); });

    const actionsBtn = screen.getByRole("button", { name: /comment actions/i });
    fireEvent.click(actionsBtn);
    fireEvent.click(screen.getByRole("button", { name: /^delete$/i }));

    expect(screen.getByText(/delete this comment/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /yes/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /no/i })).toBeInTheDocument();
  });

  it("confirms delete by clicking Yes and removes comment from list", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ items: [ownComment], nextPageToken: null }),
      })
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve({}) });
    vi.stubGlobal("fetch", fetchMock);

    render(<CommentsSection projectSlug="my-proj" issueNumber={1} me={meUser} />);
    await act(async () => { await vi.runAllTimersAsync(); });

    const actionsBtn = screen.getByRole("button", { name: /comment actions/i });
    fireEvent.click(actionsBtn);
    fireEvent.click(screen.getByRole("button", { name: /^delete$/i }));
    fireEvent.click(screen.getByRole("button", { name: /yes/i }));

    await act(async () => { await vi.runAllTimersAsync(); });

    expect(screen.queryByText("This is my comment")).toBeNull();
  });
});
