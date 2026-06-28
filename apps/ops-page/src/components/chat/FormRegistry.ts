import { FC } from "react";
import CreateProductForm from "../forms/CreateProductForm";
import type { CustomFormProps } from "../forms/types";

export type { CustomFormProps };

export const FormRegistry: Record<string, FC<CustomFormProps>> = {
  CreateProductCommand: CreateProductForm,
};
