# Blaster design notes

Decisions about Blaster's design, newest last. Each one says what was decided and why.

## Scope

- **General purpose.** Blaster generates static websites in general; blog.michael.gr is its first user,
  not its only one. The blog should end up looking very similar to today's Hugo + Stack version, but need
  not be identical.
- **Only builds, fast.** Watching and live reload belong to Cato, which serves the website directory and
  reloads open pages. Blaster's job is a build that is as fast as possible.
- **Incremental output.** Blaster never wipes the website directory: it writes only files whose content
  changed and deletes only files that disappeared. Otherwise every build looks to Cato like thousands of
  changes.
- **The web tree mirrors the disk tree.** A page's URL follows its place in the content tree (e.g.
  `post/2001/2001-09-11-wtc-disaster/index.md` → `/post/2001/2001-09-11-wtc-disaster/`). Existing URLs
  are not a design factor: whatever they break gets redirected.
- **Minimal core.** Features (tags, categories, archives, pagination, search, related posts, RSS, sitemap,
  redirects, etc.) are carried by templates and processors, not by the core. None of the features the blog
  uses today may be ruled out: each must be doable, or at least left possible for later.

## Model

- **An abstract core.** The core turns two read-only input file trees, content and template, into an
  output file tree, without knowing what kind of files they hold. It only reads input files, hands them
  to plugins for transformation, receives the transformed (or created) data, writes output files, deletes
  output files that are no longer produced, and keeps track of the plugins. Each plugin declares the file
  endings it handles, and the core builds a table from them. Plugins never touch files and do not even
  know where the directories are.
- **An orchestrator makes it a site generator.** The core has no notion of a website. An orchestrator
  plugin handles the big picture, e.g. building a static HTML site: it decides which pages exist, where
  their output goes, and whether there is an RSS feed, and produces each output itself or by calling
  other plugins. The mere presence of a template file (say, an RSS template) does not make anything run;
  the orchestrator decides. The user names the orchestrator (on the command line, or in a small file in
  the template directory).
- **Plugins call each other through the core.** That keeps them isolated, and lets the core cache: when
  a plugin's input has not changed since the last build, the core returns the previous output without
  running it.
- **Non-HTML outputs are the orchestrator's business.** RSS, the sitemap, the search index and the
  `_redirects` file are produced by the orchestrator, itself or through plugins it calls; a plain-text
  template notation may serve the simpler ones.
- **Two ways to run a plugin.** Plugins on the hot path (markdown, HTML templates) are built in and run
  in-process; external stdin/stdout processors are the extension tier for users. Same concept, same
  contract.
- **Prove it on the blog first.** Shape the interfaces for many formats, but implement only the markdown
  content plugin and the HTML template plugin at first. Building a framework for every format before one
  real site works is how projects stall.
- **HTML templates need HTML-specific constructs.** Values may be `{{…}}`, but structure (repeat, include,
  show-if) must be HTML elements or attributes, so that the template still displays properly in a browser.
  Other text formats may use a plain text notation.
- **MVVM.** The orchestrator (not the core) builds a bare view model: the site, its pages, their front
  matter and rendered content. The view model's shape is the contract between the orchestrator and the
  plugins it calls. Templates are the view; they only bind to view-model properties.
- **Processors compute everything else.** A processor is an external program, written in any language.
  The view model is piped to it as JSON on stdin; it returns the properties it adds as JSON on stdout,
  which are merged in. Each processor runs once per build, not once per page, in a declared order.
  Precedent: pandoc filters. Why: users' needs cannot be anticipated, and embedding a programming language
  in templates (as Hugo does) is exactly what Blaster avoids.
- **Templates have four constructs, no expressions:** show a field, repeat a fragment for each item of a
  collection, include a named fragment, show a fragment only if a boolean property is true. Anything that
  needs logic is a processor's job (e.g. a processor computes `page.hasToc`).
- **Templates are plain HTML** that opens in a browser.
- **No fragment files.** Every template file is a complete HTML page with all its CSS, so it looks right
  when opened in a browser; named fragments live inside such pages. Rejected: one file per fragment, as
  Hugo does, because a fragment alone has no surrounding DOM and no CSS, so it cannot be previewed. File
  size is not a concern: editors and browsers handle megabyte-sized pages.
- **One template page.** A single complete HTML page holds the shared shell (`<head>`, sidebar, footer)
  once, plus every named fragment; only the main column varies by kind of page. Rejected: several
  complete pages, because their shared parts would be copied into each and drift apart.
- **Design-time switcher.** Opened in a browser, the template shows one variant of the main column at a
  time, through a small switcher that exists only at design time. So the template needs a way to mark
  parts that never reach the output.

## Open

- The same file ending can mean different things in the two trees: `.html` in the template tree is a
  template, but in the content tree it is probably a file to copy through as is. So the file-ending table
  may need to be two tables, one per tree. Not certain, but not ruled out.
- `applies-to` chooses which template variant renders a content file as a page (by path pattern; front
  matter is not used, so templates need no knowledge of markdown). Details to discuss:
    - Ties: when several patterns match, a predictable rule is needed, e.g. first match in document order
      wins, catch-all last.
    - Patterns: proper anchored globs (`/post/**/*.md`) instead of today's unanchored regex conversion.
    - Silent breakage: a variant whose pattern matches nothing (e.g. after a file moved) should be warned
      about.
    - Possible extras: matching a view-model property as well as the path; virtual paths for pages with no
      source file (e.g. `/tags/java`), so they are routed the same way.
- The mechanism that marks design-time-only parts of the template.
- The HTML template notation. A sketch from the discussion, not decided: `{{post.title}}` for a value,
  `<article data-each="post in page.items" data-include="post-summary">` to repeat a named fragment,
  `<nav data-if="page.pager.hasPages">` to show a part conditionally.
- Where output goes: tentatively a fixed convention built into the HTML-site orchestrator, rather than an
  `output=` pattern in the template; may be revisited.
- The JSON format given to processors. It is the plugin API, so it must be designed carefully and
  versioned.
- Processors that transform content (syntax highlighting, link rendering, SVG preprocessing) rather than
  add data.
- Pages with no source file (tag pages, paginated pages, RSS, sitemap).
- Where lists come from: list files, as the README describes, or processors.

## Suggested order

1. The plugin contract: how the core hands input to plugins and gets results back, and the view model's
   JSON shape. Everything else depends on it.
2. The HTML-site orchestrator: the output convention, and pages with no source file (tag pages, pager
   pages).
3. Where lists come from.
4. The HTML template notation, the `applies-to` details, and the design-time-only mechanism.
5. Processors that transform content, and whether the file-ending table needs to be two.

## The existing code

The code predates these notes (October to December 2025) and follows the old model: the core itself
reads markdown, applies templates and decides the output. Worth keeping:

- The `FileSystem` / `FileItem` abstraction, with its in-memory fake for tests.
- The diagnostics (errors with file, line and column), including broken-link detection.
- The Markdig pipeline setup and front matter parsing (SharpYaml).
- `TemplateEngine` (`{{field}}` substitution) and its tests.

The rest (`BlasterEngine`, `View`, `ViewModel`, `content-view` / `collection-view`) is to be reworked
along these notes. `BlasterEngine.Run` still wipes the website directory, against the incremental-output
decision. Small known bugs are on card CL-162.
