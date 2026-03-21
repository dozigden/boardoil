# TODO

## Features

### Tags

Tags can be assigned to Cards.
Cards may have multiple tags.
Tags show on the board level card view, readonly.
Tags display as a small pill styled, with their text. In edit mode a small 'x' is on the pill to unassign a given tag.
Tags can be assigned to a card when in edit mode.
In edit mode a text box allows free text entry or one or more tags seperated by commas.  Pressing enter on the text box assigns that tag and clears the textbox (keeping focus).

Tags:-
 - may contain spaces
 - case sensitive
 - limit of 40 characters

There should be a main list of all tags in use, if the user free text enters a tag that doesnt exist, it is added to the list.
Tags are loosely coupled to cards, cards just have the name of the tag.

Each tag can have a different 'Style'
Initially there will be 2 Styles available.
1. Solid Style
 - single colour background
 - single colour text, or auto
 - auto text colour selected black or white text, whichever contrasts best with the background
 2. Gradient Style
 - 2 colours blended in a gradient, left to right
   - left colour
   - right colour
 - same text colour options as Solid Style

Colours are specified as html hex values in the backend.

Style should be stored against the tags in the main tag list.
When a new tag is added to the list it defaults to Solid Style, with a random background colour and auto text colour.

The main Tag list should store 
  - Tag name
  - Style Name
  - Style properties json.

The main tag list should be accessible and editable to all users in the front end.

## Quality

### Test Structure Audit

Review all test projects and align tests to a single clear `Arrange` / `Act` / `Assert` flow per test method.
Split tests that currently contain multiple independent `Act` / `Assert` phases into focused tests.

### Repository Review

Revisit `CardRow` usage in `CardRepository` and simplify the card mapping shape if possible.

### Repository Structure Follow-up

Refactor repositories toward one repository per entity type (or aggregate boundary), and introduce a generic `RepositoryBase<TEntity>` for shared CRUD/query patterns where it improves clarity.

### Bootstrap Transaction Simplification

Review `BoardBootstrapService` to determine whether default board + initial columns can be created in a single-save flow, removing the explicit transaction callback from bootstrap if equivalent behaviour can be preserved.

### Transaction Scope Audit

Review all `IDbContextScope.Transaction(...)` usages and confirm each one is justified versus a standard single-save scope.
