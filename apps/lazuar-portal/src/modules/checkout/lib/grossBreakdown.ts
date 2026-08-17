/** Hop-1 SST chrome. Mirrors Modules.Commerce.Application.SstTaxMath + GrossBreakdown. */

export const SST_NOT_APPLICABLE = "06";
export const SST_SERVICE_TAX = "02";

export type GrossBreakdown = {
  unitNet: number;
  unitTax: number;
  unitGross: number;
  seats: number;
  lineTax: number;
  gross: number;
  taxType: string;
};

/** C# MidpointRounding.AwayFromZero for non-negative money. */
export function roundMoney(amount: number): number {
  const scaled = amount * 100;
  const sign = scaled < 0 ? -1 : 1;
  return (sign * Math.round(Math.abs(scaled))) / 100;
}

export function computeSstTax(
  requestedType: string | null | undefined,
  ratePercent: number,
  netAmount: number,
  merchantHasSstRegistration: boolean,
): { taxType: string; taxAmount: number } {
  if (
    !merchantHasSstRegistration ||
    requestedType !== SST_SERVICE_TAX ||
    ratePercent <= 0 ||
    netAmount <= 0
  ) {
    return { taxType: SST_NOT_APPLICABLE, taxAmount: 0 };
  }

  return {
    taxType: SST_SERVICE_TAX,
    taxAmount: roundMoney((netAmount * ratePercent) / 100),
  };
}

/**
 * Exclusive SST on the unit, then × seats — same as SubscriptionBillingAmount.GrossBreakdown.
 * Hop-1 treats a product configured as type 02 with a rate as "SST applies".
 */
export function grossBreakdown(
  unitNet: number,
  seats: number,
  sstTaxType: string | null | undefined,
  sstRatePercent: number,
  merchantHasSst = productSignalsSst(sstTaxType, sstRatePercent),
): GrossBreakdown {
  const qty = Math.max(1, Math.trunc(seats) || 1);
  const { taxType, taxAmount: unitTax } = computeSstTax(
    sstTaxType,
    sstRatePercent,
    unitNet,
    merchantHasSst,
  );
  const unitGross = unitNet + unitTax;
  return {
    unitNet,
    unitTax,
    unitGross,
    seats: qty,
    lineTax: unitTax * qty,
    gross: unitGross * qty,
    taxType,
  };
}

export function productSignalsSst(
  sstTaxType: string | null | undefined,
  sstRatePercent: number,
): boolean {
  return sstTaxType === SST_SERVICE_TAX && sstRatePercent > 0;
}
