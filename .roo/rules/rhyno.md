# rhyno

# ALWAYS FOLLOW THESE MANDATORY DEVELOPMENT RULES
# rhyno

# ALWAYS FOLLOW THESE MANDATORY DEVELOPMENT RULES

1. **MANDATORY BUILD CHECK:**
   - Before completing ANY task or reporting success, you MUST execute `dotnet build`.
   - Never finish a task if there are compilation errors or warnings in modified files.

2. **DATABASE MIGRATION RULE:**
   - Whenever you modify EF Core Models, Entities, or DbContext, you MUST generate and apply the migration immediately (`dotnet ef database update`).
   - Always verify that new tables/columns exist in the active database.

3. **STRICT UI/UX, STYLING & FRONTEND STANDARDS:**
   - **NO BOOTSTRAP / NO JQUERY:** Strictly DO NOT use Bootstrap or jQuery. Use Pure Vanilla JS (ES6+ modules) and Tailwind CSS only.
   - **NO NATIVE/UNSTYLED HTML:** Never leave unstyled inputs, raw text lists, or default forms.
   - **INSPECT EXISTING LAYOUT FIRST:** Inspect existing working views (e.g., Dashboard, Customers) to match their exact Tailwind utility classes, CSS grid, custom Glassmorphism components, and icons.
   - **GLASSMORPHISM SYSTEM:** Maintain the core Glassmorphism design system (semi-transparent backdrops, blur filters, subtle dark/light borders).
   - **METRIC CARDS & FORMS:** Wrap inputs and forms in Tailwind styled containers. Display dashboard statistics as styled Metric Cards with proper icons, badges, and background gradients.
   - **RTL & IRANIAN FONTS:** Ensure strict RTL alignment (`dir="rtl"`), proper padding/margins, and correct Persian typography across all views.

4. **CONTEXT & MEMORY USAGE:**
   - Use the `codebase-memory` MCP tool to recall past architectural decisions, database schemas, or JS module structures before implementing major updates.

   You are allowed to execute any CMD or PowerShell command.