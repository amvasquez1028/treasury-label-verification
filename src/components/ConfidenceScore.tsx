type ConfidenceScoreProps = {
  value: number;
  label?: string;
};

const getToneClass = (value: number): string => {
  if (value >= 0.9) {
    return "verdict-badge-pass";
  }

  if (value >= 0.6) {
    return "verdict-badge-review";
  }

  return "verdict-badge-fail";
};

export const ConfidenceScore = ({ value, label = "Confidence" }: ConfidenceScoreProps) => {
  const percent = Math.round(value * 100);

  return (
    <div
      className={`inline-flex items-center gap-2 rounded-md border px-3 py-1 text-sm font-semibold ${getToneClass(value)}`}
      aria-label={`${label}: ${percent} percent`}
    >
      <span>{label}</span>
      <span>{percent}%</span>
    </div>
  );
};
