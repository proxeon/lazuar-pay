/**
 * File-backed local order store under `.data/` (gitignored).
 * Single-process demo only — not multi-instance safe.
 */
import { randomUUID } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { CreateOrderInput, Order, OrderStatus } from "./types";

const DATA_DIR = path.join(process.cwd(), ".data");
const ORDERS_FILE = path.join(DATA_DIR, "orders.json");
const DELIVERIES_FILE = path.join(DATA_DIR, "deliveries.json");

type OrdersDb = { orders: Order[] };
type DeliveriesDb = { ids: string[] };

function ensureDataDir(): void {
  if (!existsSync(DATA_DIR)) {
    mkdirSync(DATA_DIR, { recursive: true });
  }
}

function readOrders(): OrdersDb {
  ensureDataDir();
  if (!existsSync(ORDERS_FILE)) {
    return { orders: [] };
  }
  try {
    const raw = readFileSync(ORDERS_FILE, "utf8");
    const parsed = JSON.parse(raw) as OrdersDb;
    return { orders: Array.isArray(parsed.orders) ? parsed.orders : [] };
  } catch {
    return { orders: [] };
  }
}

function writeOrders(db: OrdersDb): void {
  ensureDataDir();
  writeFileSync(ORDERS_FILE, JSON.stringify(db, null, 2), "utf8");
}

function readDeliveries(): DeliveriesDb {
  ensureDataDir();
  if (!existsSync(DELIVERIES_FILE)) {
    return { ids: [] };
  }
  try {
    const raw = readFileSync(DELIVERIES_FILE, "utf8");
    const parsed = JSON.parse(raw) as DeliveriesDb;
    return { ids: Array.isArray(parsed.ids) ? parsed.ids : [] };
  } catch {
    return { ids: [] };
  }
}

function writeDeliveries(db: DeliveriesDb): void {
  ensureDataDir();
  writeFileSync(DELIVERIES_FILE, JSON.stringify(db, null, 2), "utf8");
}

export function createOrder(input: CreateOrderInput): Order {
  const now = new Date().toISOString();
  const currency = (input.currency ?? "MYR").trim().toUpperCase() || "MYR";
  const order: Order = {
    id: randomUUID(),
    amount: input.amount,
    currency,
    description: (input.description ?? "Sample order").trim() || "Sample order",
    customerEmail: input.customerEmail.trim(),
    status: "draft",
    metadata: {
      type: "sample_order",
      source: "hub-cashier-next",
    },
    createdAt: now,
    updatedAt: now,
  };

  const db = readOrders();
  db.orders.unshift(order);
  writeOrders(db);
  return order;
}

export function getOrder(id: string): Order | undefined {
  return readOrders().orders.find((o) => o.id === id);
}

export function listOrders(): Order[] {
  return readOrders().orders;
}

export function findByCheckoutId(checkoutId: string): Order | undefined {
  return readOrders().orders.find((o) => o.hubCheckoutId === checkoutId);
}

export function updateOrder(
  id: string,
  patch: Partial<
    Pick<
      Order,
      | "status"
      | "hubCheckoutId"
      | "checkoutUrl"
      | "paidAt"
      | "lastDeliveryId"
      | "lastEventId"
      | "gatewayTransactionId"
      | "metadata"
    >
  >,
): Order | undefined {
  const db = readOrders();
  const idx = db.orders.findIndex((o) => o.id === id);
  if (idx < 0) return undefined;
  const current = db.orders[idx]!;
  const next: Order = {
    ...current,
    ...patch,
    metadata: patch.metadata ? { ...current.metadata, ...patch.metadata } : current.metadata,
    updatedAt: new Date().toISOString(),
  };
  db.orders[idx] = next;
  writeOrders(db);
  return next;
}

export function setOrderStatus(
  id: string,
  status: OrderStatus,
  extra?: Partial<Order>,
): Order | undefined {
  return updateOrder(id, { status, ...extra });
}

/** Delivery-id dedupe (file-backed Set). Multi-instance: not shared. */
export function hasSeenDelivery(deliveryId: string): boolean {
  if (!deliveryId) return false;
  return readDeliveries().ids.includes(deliveryId);
}

export function markDeliverySeen(deliveryId: string): void {
  if (!deliveryId) return;
  const db = readDeliveries();
  if (db.ids.includes(deliveryId)) return;
  db.ids.push(deliveryId);
  // Cap growth for long demos
  if (db.ids.length > 500) {
    db.ids = db.ids.slice(-400);
  }
  writeDeliveries(db);
}

export function storePathHint(): string {
  return ".data/orders.json (and .data/deliveries.json)";
}
