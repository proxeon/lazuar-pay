import { FC } from "react";
import CreatePlanForm from "../forms/CreatePlanForm";
import type { CustomFormProps } from "../forms/types";

export type { CustomFormProps };

export const FormRegistry: Record<string, FC<CustomFormProps>> = {
  CreatePlanCommand: CreatePlanForm,
};
