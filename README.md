# Blaster

## A static website generator for markdown<br>(because Hugo is a monstrosity)

<img src="logo.svg" width="200em" style="display: block; margin: 0 auto;"/>

Blaster is a static website generator for markdown content.

## The directories

Blaster operates on three directories:

- **The content directory**

  Contains markdown files and associated media files.

  At the root of the content directory there must be a markdown file called `index.md`. This is known as the content root. All content files must be reachable from the root either via markdown references or via implicit lists (see below.) Blaster will issue a warning for each content file that cannot be reached from the root.
  
  Each content file is identified by its content-file-pathname, which is normalized (contains no dot-directories) rooted (begins with a slash) and relative to the root of the content directory.

  The content directory is treated as read-only by Blaster.
  
- **The template directory**
  
  Contains the html template and associated media files. The html template must be a single html file called   `template.html`. This html file defines the root template and contains sub-templates.

  The template directory is treated as read-only by Blaster.
  
- **The website directory**

  This is where Blaster generates html files and copies media files. 
  
  After the initial generation of html files, blaster can keep running in 'watcher' mode. In this mode, blaster will  keep listening for changes in the content and template directories, and when any file gets modified, blaster will  update any and all files in that need updating in the website directory.

  The website directory is treated as read-write by Blaster.

## More about markdown files

There are two kinds of markdown files:
  
  1. **Document files**. A document file is any markdown file that does not fit the description of a list. (See below.)  It will become a separate HTML page in the generated website.
  
  1. **List files**. A list file is a markdown file that defines a list of content files. A list file will not become an HTML page; however, a markdown reference to a list file will be rendered inside a html file using a special kind of template known as a "list view"; more on that below.

  The root markdown file can be either a document file or a list file.

## More about lists

A list can be defined in one of two ways:

 - Explicit list file
 
   This is a markdown file which contains nothing but markdown references to other content files after the front matter. (Whitespace and comments are ignored.) This file defines a list of explicitly referenced files. The referenced files can reside anywhere within the content directory.

 - Implicit list file

   This is a markdown file which contains nothing after the front matter. (Whitespace and comments are ignored.) It defines a list comprising all content files (except itself) whose content-file-pathnames match a certain pattern (configured via front-matter) and reside in the same directory and optionally (also configured via front-matter) all subdirectories recursively.

## Markdown references

A reference in markdown can be either external or internal.

- An external markdown reference is any markdown reference that begins with a protocol, such as `http://` or `https://`. Such a reference points to a resource outside the content directory.

- An internal markdown reference is any markdown reference that does not begin with a protocol, and therefore points to a file within the content directory.
 
An internal markdown reference must be specified as being relative to the markdown file that contains it, but this is only due to limitations of existing tooling, such as Obsidian. We might introduce an additional convention where absolute references (starting with a slash) are also valid, and they are treated as relative to the root of the content directory. In any case, blaster will always convert an internal markdown reference to a content-file-pathname by performing the following operations:

  - convert it from relative to absolute by prepending to it the location of the containing markdown file
  - normalize it (remove dot-directories)
  - convert it again from absolute to relative with respect to the root of the content directory
  - prepend a slash

Blaster will always generate an error if an internal reference targets a file that does not exist.

## Mappings

Each view specifies a set of mappings. A mapping specifies which sub-view should be used to render into html a certain kind of content.

A mapping uses a regular expression to match content-file-pathnames, so that many content files can be mapped to the same view.

There are a few different kinds of mappings: 

1. External reference mapping

   Specifies the view to use to emit html for external references. If not specified, the default external reference view will emit an `<a...>` tag that opens the referenced external resource in a new browser tab or window. The mapping uses a regular expression to select which references to apply to; this allows us to define different views for different types of external resources, such as document files, image files, video files, audio files, etc.

1. Internal document reference mapping

   Specifies the view to use to emit an internal reference to a markdown file. If not specified, the default internal document reference view will emit an `<a...>` tag.

1. Internal media reference mapping

   Specifies the view to use to emit html for an internal reference to a non-markdown (non-document and non-list) file. A few internal media reference views are predefined, for example one which matches all common image media types and emits an `<img ...>` tag, and one which matches all other media types and emits an `<a ...>` tag.

1. List mapping

   Specifies the view to use to emit html for an internal reference to a list file. If not defined, the default list view is used. The default list view simply emits a list of `<a...>` tags.

## Views

The easiest way to define a view is via an html template.

## TODO:

- Plugins (additional views)
- Functionality that must somehow be achievable:
  - List of posts (possibly with pagination)
  - Search and search results (possibly with pagination)
  - List of post tags/categories with each post
  - List of all categories (on the sidebar)
  - List of all tags (tag cloud?) (on the sidebar)
- Add minification of css, js files
- Add embedding of small svg files
- Research the "integrity" attribute of `<script>` and possibly implement it
- Research the "srcset" attribute of `<img>` and possibly implement it

## IDEAS:

- Introduce a `<content>` element in template html, to contain all child templates. When this element gets extracted from the template, it gets replaced with `{{content}}`, so that once the resolved child template has been applied, we know exactly where to paste the result. Also, the html inside this element and between the child templates gets completely stripped away, so the web designer can place some design-time-only html there to better organize the child templates.


* * *

Logo: based on ["Gun" by Simon Child from The Noun Project](https://thenounproject.com/icon/gun-80261/)
