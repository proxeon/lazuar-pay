
# ADR 012: Sidebar Navigation Mechanics (The Super App Console)

**Status:** Active  
**Context:** As the Lazuar platform transitions into a unified "Super App" Console housing multiple modules (Community, Vault, Funnel, etc.), the sidebar must scale gracefully. It needs to handle dozens of links without causing cognitive overload, while supporting a sleek 48px collapsed state for power users.

This document outlines the strict UI mechanics and architectural rules used in `Sidebar.tsx`.

## 1. The "48px Anchor" Rule (Preventing Jitter)

**The Problem:** When transitioning a sidebar from expanded (240px) to collapsed (48px), icons tend to jump or shift horizontally if padding or flex-gaps are used dynamically. 
**The Solution:** All icons in the sidebar (both Module icons and the User Profile avatar) must be permanently locked inside a strict 48-pixel wide container.

```tsx
// STRICT ANCHOR PATTERN
<div className="w-12 h-full shrink-0 flex items-center justify-center">
  <Icon size={16} />
</div>
```
*   **Mechanic:** The icon wrapper *never* changes size. When the sidebar expands, we reveal the text next to it by animating the text-wrapper's opacity and width. Because the icon's container is static (`w-12 shrink-0`), the icon remains perfectly anchored to the left edge during the Framer Motion layout transition.

## 2. Module Navigation (`ModuleNav` Component)

To prevent the sidebar from becoming a massive vertical list of 100+ links, navigation is grouped by Module (e.g., Community). The `ModuleNav` component acts as a smart wrapper that fundamentally changes its interaction pattern based on the sidebar's current width.

### A. Expanded State (The Accordion)
When the sidebar is fully expanded (240px):
*   **Behavior:** Clicking the module header acts as an accordion. It smoothly pushes the content down using `Framer Motion` (`animate={{ height: "auto" }}`).
*   **Auto-Expansion:** If the user's current URL matches the module's `basePath` (e.g., `/community/subscribers`), the accordion automatically mounts in the `open` state so the user always has context of where they are.
*   **Visual Hierarchy:** The child links are indented (`pl-[48px]`) to align perfectly with the text of the parent module, maintaining a clean vertical reading line.

### B. Collapsed State (The Flyout)
When the sidebar is collapsed (48px):
*   **Behavior:** Accordions cannot work in a 48px sidebar because expanding them pushes other icons down with no context. Instead, clicking the module icon spawns an absolute-positioned floating **Flyout Menu** to the right (`left-[calc(100%+8px)]`).
*   **Click-to-Open (Not Hover):** To ensure mobile/touch compatibility and prevent the "diagonal tracking" issue (where the menu disappears if the mouse slips by 1 pixel), the flyout is strictly state-driven (`isFlyoutOpen`). It requires a deliberate click to open.
*   **Auto-Close:** The flyout utilizes a `useRef` and a `mousedown` event listener attached to the `document`. If the user clicks anywhere outside the menu, it instantly closes.

## 3. CSS Overflow & Z-Index Management

**The Trap:** It is tempting to put `overflow-y-auto` on the sidebar container so it scrolls if the user has a small screen. 
**The Rule:** You **must not** apply `overflow-y-auto` or `overflow-hidden` to the middle section of the sidebar.
*   *Why:* If the container has `overflow`, any absolute-positioned elements (like the Collapsed Flyout Menu) will be clipped by the container's bounding box and become invisible. The flyout must be allowed to break out of the sidebar's DOM constraints, which requires visible overflow.

## 4. DOM Stability (No Conditional Swapping)

**The Rule:** Never use React conditional rendering (`if (expanded) return <Expanded/> else return <Collapsed/>`) for the trigger buttons.
*   *Why:* Completely destroying and recreating DOM nodes during a transition prevents CSS and Framer Motion from interpolating the layout. 
*   *Implementation:* We use a single, persistent `<button>` for the trigger. We conditionally animate the *children* (hiding the text, showing the flyout vs. showing the accordion), ensuring the parent DOM node remains stable and animating smoothly.

## 5. Mobile Behavior
On mobile devices (`isMobile = true` via `useIsMobile` hook):
*   The 48px collapsed state is disabled. The sidebar is either fully expanded (240px) or completely hidden (translated off-screen via `x: -240`).
*   A black backdrop (`bg-black/10 backdrop-blur-sm`) renders over the main content area. Clicking this backdrop closes the sidebar.
*   Clicking any navigational link inside the sidebar automatically triggers `setIsOpen(false)` to close the menu and return the user to the content.
