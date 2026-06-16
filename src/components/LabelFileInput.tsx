"use client";

import { ChangeEvent, useId, useRef } from "react";

type LabelFileInputProps = {
  label: string;
  accept?: string;
  fileName?: string | null;
  onChange: (event: ChangeEvent<HTMLInputElement>) => void;
  onRemove?: () => void;
  ariaLabel?: string;
};

export const LabelFileInput = ({
  label,
  accept = "image/png,image/jpeg",
  fileName,
  onChange,
  onRemove,
  ariaLabel,
}: LabelFileInputProps) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const inputId = useId();

  const handleChooseFile = () => {
    inputRef.current?.click();
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      inputRef.current?.click();
    }
  };

  const handleRemove = () => {
    if (inputRef.current) {
      inputRef.current.value = "";
    }
    onRemove?.();
  };

  return (
    <div className="space-y-2">
      <span className="block text-sm font-semibold text-[var(--color-base-darkest)]">{label}</span>
      <input
        ref={inputRef}
        id={inputId}
        type="file"
        accept={accept}
        onChange={onChange}
        className="sr-only"
        aria-label={ariaLabel ?? label}
        tabIndex={-1}
      />
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={handleChooseFile}
          onKeyDown={handleKeyDown}
          className="inline-flex rounded border border-[var(--color-primary-dark)] bg-white px-4 py-2 text-sm font-semibold text-[var(--color-primary-darker)] hover:bg-[var(--color-base-lighter)] focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-[var(--color-primary-dark)]"
          aria-controls={inputId}
        >
          Choose file
        </button>
        {fileName && onRemove ? (
          <button
            type="button"
            onClick={handleRemove}
            className="rounded border border-red-300 px-3 py-2 text-sm font-semibold text-red-800 hover:bg-red-50 focus:outline focus:outline-2 focus:outline-offset-2 focus:outline-red-400"
            aria-label={`Remove selected file ${fileName}`}
          >
            Remove image
          </button>
        ) : null}
      </div>
      {fileName ? (
        <p className="text-xs text-[var(--color-base-darkest)]">Selected: {fileName}</p>
      ) : (
        <p className="text-xs text-[var(--color-base)]">No file selected</p>
      )}
    </div>
  );
};
