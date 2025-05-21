export type ValidationEventData = {
  errors: Record<string, string[]>;
};

export type ProgressEventData = {
  current: number;
  total: number;
  color: string;
};

export type CompletedEventData = {
  success: true;
};

export type SseEvent =
  | {
      type: "error";
      data: string;
    }
  | {
      type: "validation";
      data: ValidationEventData;
    }
  | {
      type: "progress";
      data: ProgressEventData;
    }
  | {
      type: "completed";
      data: CompletedEventData;
    };
