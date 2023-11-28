## Notes to self

To reset the public API file:

```bash
find . -type f -name 'PublicAPI.Shipped.txt' -exec sh -c 'echo "#nullable enable" > "$0"' {} \;

Go to a file in each project, do fix all just for that project on that api error, then Save All. Duplicates will occur if not careful. 

Then run find . -type f -name 'PublicAPI.Unshipped.txt' -exec sh -c 'cat "$1" >> "${1%Unshipped.txt}Shipped.txt" && > "$1"' _ {} \;

to move these to the shipped file. 

```

Note: Roslynator didn't work on those fixes, had to use IDE>

