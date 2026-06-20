// apps/ops-page/src/components/chat/FormRegistry.ts
import { FC } from "react";
import CreatePlanForm from "./CreatePlanForm";

export interface CustomFormProps {
  prefillData?: Record<string, any>;
  onSubmit: (data: Record<string, any>) => void;
  onCancel: () => void;
}

export const FormRegistry: Record<string, FC<CustomFormProps>> = {
  CreatePlanCommand: CreatePlanForm,
};
