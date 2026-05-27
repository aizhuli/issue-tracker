import { render, screen, fireEvent, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { CreateIssueModal } from "@/components/issues/CreateIssueModal";
import type { IssueFull } from "@/lib/types/issues";

vi.mock("react-dom", async () => {
  const actual = await vi.importActual<typeof import("react-dom")>("react-dom");
  return { ...actual, createPortal: (node: React.ReactNode) => node };
});

const mockIssue: IssueFull = {
  id: "issue-1",
  number: 1,
  displayKey: "PROJ-1",
  title: "Fix login bug",
  description: null,
  status: "backlog",
  priority: "medium",
  assignee: null,
  reporter: { id: "user-1", name: "Alice", email: "alice@example.com" },
  labels: [],
  acceptanceCriteria: null,
  acceptanceCriteriaAiSuggested: false,
  commentCount: 0,
  createdAt: "2024-01-01T00:00:00Z",
  updatedAt: "2024-01-01T00:00:00Z",
  closedAt: null,
};

function renderModal(overrides: Partial<Parameters<typeof CreateIssueModal>[0]> = {}) {
  const defaults = {
    open: true,
    projectSlug: "my-project",
    onClose: vi.fn(),
    onCreated: vi.fn(),
  };
  return render(<CreateIssueModal {...defaults} {...overrides} />);
}

describe("CreateIssueModal", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("renders modal with title input when open", () => {
    renderModal();
    expect(screen.getByPlaceholderText(/issue title/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /create/i })).toBeInTheDocument();
  });

  it("does not render when open is false", () => {
    renderModal({ open: false });
    expect(screen.queryByPlaceholderText(/issue title/i)).toBeNull();
  });

  it("Create button is disabled when title is empty", () => {
    renderModal();
    expect(screen.getByRole("button", { name: /create/i })).toBeDisabled();
  });

  it("Create button is enabled when title has text", () => {
    renderModal();
    fireEvent.change(screen.getByPlaceholderText(/issue title/i), {
      target: { value: "New Issue" },
    });
    expect(screen.getByRole("button", { name: /create/i })).not.toBeDisabled();
  });

  it("Create button is disabled while submitting", async () => {
    let resolveFetch!: (value: unknown) => void;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockReturnValue(
        new Promise((resolve) => {
          resolveFetch = resolve;
        })
      )
    );

    renderModal();
    fireEvent.change(screen.getByPlaceholderText(/issue title/i), {
      target: { value: "New Issue" },
    });

    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /create/i }));
    });

    // While fetch is pending, the button shows a spinner (no text) and is disabled
    // Find the submit button by its disabled state — it's the only button in the modal
    const buttons = screen.getAllByRole("button");
    const submitBtn = buttons[buttons.length - 1];
    expect(submitBtn).toBeDisabled();

    // Resolve the fetch to clean up
    resolveFetch({ ok: true, json: () => Promise.resolve(mockIssue) });
    await act(async () => { await vi.runAllTimersAsync(); });
  });

  it("title-only POST on submit", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(mockIssue),
    });
    vi.stubGlobal("fetch", fetchMock);

    renderModal();
    fireEvent.change(screen.getByPlaceholderText(/issue title/i), {
      target: { value: "Fix login bug" },
    });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /create/i }));
      await vi.runAllTimersAsync();
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/projects/my-project/issues",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ title: "Fix login bug" }),
      })
    );
  });

  it("calls onCreated with the created issue on success", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockIssue),
      })
    );

    const onCreated = vi.fn();
    renderModal({ onCreated });

    fireEvent.change(screen.getByPlaceholderText(/issue title/i), {
      target: { value: "Fix login bug" },
    });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /create/i }));
      await vi.runAllTimersAsync();
    });

    expect(onCreated).toHaveBeenCalledWith(mockIssue);
  });

  it("calls onClose after successful creation", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockIssue),
      })
    );

    const onClose = vi.fn();
    renderModal({ onClose });

    fireEvent.change(screen.getByPlaceholderText(/issue title/i), {
      target: { value: "Fix login bug" },
    });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /create/i }));
      await vi.runAllTimersAsync();
    });

    expect(onClose).toHaveBeenCalled();
  });

  it("Enter key in title input submits the form", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(mockIssue),
    });
    vi.stubGlobal("fetch", fetchMock);

    renderModal();
    const titleInput = screen.getByPlaceholderText(/issue title/i);
    fireEvent.change(titleInput, { target: { value: "My Issue" } });

    await act(async () => {
      fireEvent.keyDown(titleInput, { key: "Enter" });
      await vi.runAllTimersAsync();
    });

    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it("shows error message on failure response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 422,
        json: () =>
          Promise.resolve({
            errorCode: "issues:issue:title:required_or_too_long",
            detail: "Title is too long",
          }),
      })
    );

    renderModal();
    fireEvent.change(screen.getByPlaceholderText(/issue title/i), {
      target: { value: "Some title" },
    });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /create/i }));
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText("Title is too long")).toBeInTheDocument();
  });
});
