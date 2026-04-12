# Meal Prep UI Brief

## Purpose

This document is a UI and UX brief for implementing the Meal Prep product as a sleek, friendly, professional web application with a polished but restrained visual style.

The intended result is not a generic dashboard. The UI should feel calm, modern, efficient, and delightful to use for everyday household planning. The app should especially excel in the two high-focus mobile experiences:

- cooking mode
- shopping mode

The overall product should feel trustworthy and professional, but not sterile. It should feel warm enough for home use and sharp enough to feel high quality.

## Design Goals

- Make meal planning feel simple and low-friction.
- Make recipe capture feel clean and organized.
- Make shopping and cooking modes obviously optimized for real-world use.
- Keep the interface visually refined without becoming ornamental or heavy.
- Use motion to improve clarity and delight, not to show off.

## Product Personality

The app should feel:

- friendly
- capable
- calm
- organized
- modern
- lightly premium

It should not feel:

- corporate enterprise software
- overly playful or childish
- dark, moody, or aggressive
- overly dense
- template-like or generic

## Visual Direction

### Overall look

Aim for a clean editorial-meets-product feel. The interface should combine:

- soft surfaces
- strong spacing rhythm
- clear typography hierarchy
- subtle depth
- carefully chosen accent color

The design should feel brighter and more human than a typical admin UI.

### Color

Use a light theme as the primary visual direction.

Recommended palette characteristics:

- warm or neutral base backgrounds
- slightly tinted panels instead of stark pure white everywhere
- one fresh accent color for key actions and highlights
- strong readable text contrast
- subtle success/warning/error states

Avoid:

- purple-heavy default SaaS styling
- harsh black-on-white everywhere
- neon accents
- too many competing colors

Possible color direction:

- background: warm off-white or very light stone
- cards: soft white or lightly tinted cream/gray
- accent: fresh green, herb tone, olive, tomato, or muted citrus
- text: deep charcoal, not true black

The product is about food and planning, so it is reasonable for the palette to quietly reference freshness and ingredients without becoming themed or kitschy.

### Typography

Typography should be expressive but practical.

- Use a more intentional type choice than default system or standard SaaS styling.
- Headings should feel distinctive and polished.
- Body text must remain highly readable on mobile.
- Numerals and quantities should be easy to scan in ingredient lists.

Visual hierarchy should clearly separate:

- page-level context
- section titles
- recipe titles
- metadata
- ingredients
- step content
- supportive helper text

### Shape and surfaces

- Prefer rounded corners, but keep them disciplined and consistent.
- Use cards and panels with subtle elevation or tonal separation.
- Avoid excessive borders.
- Use soft shadows and layered backgrounds sparingly.

## UX Priorities

### 1. Recipe library

The recipe library should feel pleasant to browse and fast to search.

Requirements:

- strong search
- easy scanning of recipe cards or rows
- clear recipe metadata
- easy entry points for create and import
- smooth transition into recipe detail

Suggested feel:

- visually rich enough to feel curated
- restrained enough that large libraries still feel manageable

### 2. Weekly planner

The weekly planner is one of the central surfaces and should feel effortless.

Requirements:

- a clear week overview
- fast add flow for recipes
- ability to see unplanned gaps immediately
- clear indication of today and upcoming meals
- strong mobile adaptation

Suggested feel:

- structured like a calm planning board
- not overly spreadsheet-like
- readable at a glance

### 3. Shopping list

The standard shopping-list page should support review, editing, and preparation before entering shopping mode.

Requirements:

- generated list grouped in a way that feels organized
- good support for merged ingredients and adjusted quantities
- easy manual editing
- clear source traceability from recipes or planned meals

Suggested feel:

- practical and editable
- clean enough that the user trusts the generated output

### 4. Cooking mode

Cooking mode should feel like a focused tool, not a standard app page.

Requirements:

- minimal chrome
- large readable steps
- big touch targets
- strong current-step emphasis
- easy next/previous navigation
- easy access to ingredients and timers
- works well one-handed or from a counter at a short distance

Suggested feel:

- immersive
- confident
- frictionless

### 5. Shopping mode

Shopping mode should feel optimized for movement, speed, and distraction resistance.

Requirements:

- oversized tap targets
- clear completion states
- easy hiding of completed items
- strong progress visibility
- fast interaction on mobile
- legible while walking through a store

Suggested feel:

- lightweight
- responsive
- fast
- obvious

## Interaction Design

### General interaction principles

- Keep primary actions obvious.
- Reduce visual clutter around decision points.
- Prefer progressive disclosure over dumping all controls at once.
- Use inline editing where it improves speed, but avoid accidental edits.
- Make generated content easy to review and correct.

### Navigation

The app should have a clear information architecture around:

- recipes
- planner
- shopping lists
- settings

Workspace context should be visible but not visually dominant.

Navigation should feel clean and modern:

- desktop can use a polished sidebar or combined header/sidebar pattern
- mobile should simplify navigation and prioritize current tasks

### Forms

Forms should feel light and high-confidence.

- Inputs should be roomy and easy to parse.
- Long recipe forms should be broken into clear sections.
- Ingredient entry should feel especially efficient.
- Inline validation should be calm and helpful.

### Empty states

Empty states should be useful and encouraging.

- Explain what the user can do next.
- Offer one clear primary action.
- Avoid cartoonish illustration-heavy patterns unless they fit the visual language.

## Motion And Animation

Use a little animation to create delight and improve continuity, but keep it professional.

### Motion principles

- fast
- subtle
- intentional
- smooth
- never distracting

### Good animation candidates

- page or section fade-and-slide on load
- staggered entrance for cards or list rows
- smooth transitions between planner states
- check-off animations in shopping mode
- step transitions in cooking mode
- hover and press feedback on key interactive controls
- expandable sections for ingredients, notes, and recipe metadata

### Avoid

- exaggerated bounce
- slow modal animations
- attention-seeking floating effects
- motion on every element
- decorative animation unrelated to user intent

### Motion tone

Animations should communicate polish and responsiveness, not novelty.

## Layout Guidance

### Desktop

Desktop should support comfortable planning and editing.

- Use spacious layouts.
- Give major flows room to breathe.
- Avoid cramming multiple dense panels into a single screen without hierarchy.
- Recipe detail, planner, and shopping views should each have a clear focal area.

### Mobile

Mobile is critical and should not feel like a fallback.

- Design mobile-first for cooking mode and shopping mode.
- Use sticky controls where helpful.
- Respect thumb reach.
- Keep important actions within easy reach.
- Avoid tiny segmented controls or dense tables in mobile flows.

## Key Screens To Design Well

- recipe library
- recipe detail
- recipe create and edit
- import from URL review
- weekly planner
- shopping list detail
- shopping mode
- cooking mode

## Screen-Level Intent

### Recipe library

Should feel browsable, tidy, and useful whether the user has 5 recipes or 500.

### Recipe detail

Should feel beautifully structured, with ingredients, steps, metadata, and nutrition info clearly separated without clutter.

### Recipe create and edit

Should make structured recipe entry feel manageable rather than form-heavy.

### Import review

Should make the imported data look trustworthy while clearly showing what can be corrected.

### Weekly planner

Should feel like the operational center of the product.

### Shopping list detail

Should support confidence before the user goes to the store.

### Shopping mode

Should feel purpose-built for active use in a store.

### Cooking mode

Should feel purpose-built for active use in the kitchen.

## Nutrition UI Guidance

Recipes can include nutrition information. The UI should support this without making every recipe screen feel clinical.

Requirements:

- nutrition should be optional
- if present, it should be neatly organized and easy to scan
- it should be visually secondary to ingredients and steps
- values should be tied to servings or per-serving context

Suggested presentation:

- a compact nutrition card or grouped facts section
- clean label-value formatting
- avoid over-styled “fitness app” aesthetics

## Accessibility And Usability

- Maintain strong color contrast.
- Ensure tap targets are comfortable on mobile.
- Do not rely on color alone for status.
- Preserve readability in ingredient quantities and step instructions.
- Support keyboard navigation on desktop flows.
- Motion should respect reduced-motion preferences.

## Content Tone

Microcopy should be:

- concise
- clear
- helpful
- warm but not chatty

Examples of tone:

- “Import recipe”
- “Plan this meal”
- “Start shopping”
- “Start cooking”
- “Review imported ingredients”

Avoid overly technical or generic labels where a clearer product phrase exists.

## Implementation Expectations For The UI Agent

The UI should:

- feel custom and intentional, not like an untouched component library
- preserve consistency across recipe, planner, shopping, and cooking flows
- use a few tasteful animations for load, transition, and completion states
- prioritize mobile UX for shopping and cooking
- maintain a professional standard suitable for a polished consumer product

## Creative Direction Summary

Design a sleek, friendly meal-planning app that feels polished and modern, with calm premium styling, excellent mobile task flows, and subtle animation that makes the experience feel responsive and delightful rather than flashy.
