# Blaster

## A static website generator for markdown<br>(because Hugo is a monstrosity)

<img src="logo.svg" width="200em" style="display: block; margin: 0 auto;"/>

You specify a directory with markdown files.  One of the files should be called index.md (configurable) and it is 
known as the root.

There are two kinds of markdown files: content files, and list files.

1. Content file

   This is any markdown file with content that does not fit the description of implicit or explicit lists. (See below.)
   It stands for content that will become a separate HTML page.

1. List file

   List files come in two flavors: implicit and explicit.

   - Implicit list file

      This is a markdown file which contains nothing after the front matter. It defines a list of markdown files, which 
	  includes all markdown files in the same directory, (excluding itself,) and all subdirectories, recursively.

   - Explicit list file
 
      This is a markdown file which, after the front matter, contains nothing but links to other markdown files, with 
	  arbitrary whitespace between the links. It defines a list of referenced markdown files.

   A list will not become an HTML page, but a link to the list file is treated as a vector list, as opposed to a 
   singular list.  A vector list can be used as input to some control that will display a list of content items.

All markdown files must be reachable from the root. If any markdown files cannot be reached from the root, (either
implicitly or explicitly,) Blaster will issue a warning for each of them.

Content files and lists are addressable using identifiers.

- The identifier of a content file is the relative path from the root to the file, including the filename but excluding 
the .md extension.

- The identifier of a list is the relative path from the root to the list file.

In the root you must specify a mapping file which maps views (html sections) to identifiers. The mapping is done using a
regular expression for the identifier, so that all identifiers under a certain directory can be mapped to the same view. 

Additionally, you can specify a few special mappings: 

1. Image mappings

   An image mapping defines the view to use to emit `<img>` elements. If not specified, a plain `<img ...>` tag is
   emitted.

1. Link mappings

   A link mapping defines the view to use to emit `<a>` elements. If not specified, a plain `<a...>` tag is emitted. 
   There are a couple of variants:

   - Internal link mappings
	
	 An internal link mapping defines the view to use to emit relative URLs. (Links pointing to content files within the site.)

   - External link mapping

     An external link mapping defines the view to use to emit absolute URLs to resources outside of the site.

1. List mappings

   A list mapping defines the view to use to emit a list.

TODO:

- Functionality to implement:
  - List of posts (possibly with pagination)
  - Search and search results (possibly with pagination)
  - List of post tags/categories with each post
  - List of all categories (on the sidebar)
  - List of all tags (tag cloud?) (on the sidebar)
- Add minification of css, js files
- Add embedding of small svg files
- Research the "integrity" attribute of `<script>` and possibly implement it
- Research the "srcset" attribute of `<img>` and possibly implement it

IDEAS:

- Introduce a `<content>` element in template html, to contain all child templates. When this element gets extracted from the template, it gets replaced with `{{content}}`, so that once the resolved child template has been applied, we know exactly where to paste the result. Also, the html inside this element and between the child templates gets completely stripped away, so the web designer can place some design-time-only html there to better organize the child templates.


* * *

Logo: based on ["Gun" by Simon Child from The Noun Project](https://thenounproject.com/icon/gun-80261/)
