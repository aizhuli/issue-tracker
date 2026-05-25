"use client";

import { ReactNode, useEffect, useRef } from "react";
import ReactDOM from "react-dom";

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';

interface ModalProps {
  open: boolean;
  onClose: () => void;
  labelledBy?: string;
  children: ReactNode;
}

export function Modal({ open, onClose, labelledBy, children }: ModalProps) {
  // The inner dialog card — used for focus management and focus trap
  const cardRef = useRef<HTMLDivElement>(null);
  // Remember what was focused before opening so we can restore on close
  const returnFocusRef = useRef<Element | null>(null);

  // Capture the currently focused element when modal opens
  useEffect(() => {
    if (open) {
      returnFocusRef.current = document.activeElement;
    }
  }, [open]);

  // Focus first focusable element inside modal when it opens; restore on close
  useEffect(() => {
    if (!open) {
      if (returnFocusRef.current instanceof HTMLElement) {
        returnFocusRef.current.focus();
      }
      return;
    }

    const card = cardRef.current;
    if (!card) return;

    const focusables = Array.from(card.querySelectorAll<HTMLElement>(FOCUSABLE));
    if (focusables.length > 0) {
      focusables[0].focus();
    }
  }, [open]);

  // Keyboard handling: Esc to close + focus trap
  useEffect(() => {
    if (!open) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        onClose();
        return;
      }

      if (e.key === "Tab") {
        const card = cardRef.current;
        if (!card) return;

        const focusables = Array.from(
          card.querySelectorAll<HTMLElement>(FOCUSABLE)
        );
        if (focusables.length === 0) return;

        const first = focusables[0];
        const last = focusables[focusables.length - 1];

        if (e.shiftKey) {
          // Shift+Tab at first element → wrap to last
          if (document.activeElement === first) {
            e.preventDefault();
            last.focus();
          }
        } else {
          // Tab at last element → wrap to first
          if (document.activeElement === last) {
            e.preventDefault();
            first.focus();
          }
        }
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  return ReactDOM.createPortal(
    <>
      {/* Scrim — full-viewport backdrop; clicking it closes the modal */}
      <div className="ai-scrim" onClick={onClose} aria-hidden="true" />

      {/* Centered wrapper — positions the card; stops clicks from reaching scrim */}
      <div className="ai-modal">
        <div
          ref={cardRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={labelledBy}
          tabIndex={-1}
          onClick={(e) => e.stopPropagation()}
        >
          {children}
        </div>
      </div>
    </>,
    document.body
  );
}
