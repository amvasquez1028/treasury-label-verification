"use client";

import { useEffect, useRef } from "react";

type AutoGrowTextareaProps = {
  id?: string;
  value: string;
  onChange: (event: React.ChangeEvent<HTMLTextAreaElement>) => void;
  onFocus?: () => void;
  placeholder?: string;
  className?: string;
  "aria-label"?: string;
};

export const AutoGrowTextarea = ({
  id,
  value,
  onChange,
  onFocus,
  placeholder,
  className,
  "aria-label": ariaLabel,
}: AutoGrowTextareaProps) => {
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    const element = textareaRef.current;
    if (!element) {
      return;
    }

    element.style.height = "auto";
    element.style.height = `${element.scrollHeight}px`;
  }, [value]);

  return (
    <textarea
      ref={textareaRef}
      id={id}
      value={value}
      onChange={onChange}
      onFocus={onFocus}
      placeholder={placeholder}
      rows={1}
      className={className}
      aria-label={ariaLabel}
    />
  );
};
