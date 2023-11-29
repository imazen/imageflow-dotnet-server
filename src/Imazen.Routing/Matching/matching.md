# Design of route matcher syntax



/images/{seo_string_ignored}/{sku:guid}/{image_id:int:range(0,1111111)}{width:int:after(_):optional}.{format:only(jpg|png|gif)}
/azure/centralstorage/skus/${sku}/images/${image_id}.${format}?format=avif&w=${width}


/images/{path:*:has_supported_image_type}
/azure/container/${path}









We only want non-backtracking functionality. 
all conditions are AND, and variable strings are parsed before conditions are applied, with the following exceptions:
after, until.
If a condition lacks until, it is taken from the following character.



Variables in match strings will be 
{name:condition1:condition2}
They will terminate their matching when the character that follows them is reached. We explain that variables implictly match until their last character

"/images/{id:int:until(/):optional}seoname"
"/images/{id:int}/seoname"
"/image_{id:int}_seoname"
"/image_{id:int}_{w:int}_seoname"
"/image_{id:int}_{w:int:until(_):optional}seoname"
"/image_{id:int}_{w:int:until(_)}/{**}"

A trailing ? means the variable (and its trailing character (leading might be also useful?)) is optional.

Partial matches
match_path="/images/{path:**}"
remove_matched_part_for_children

or
consume_prefix "/images/"


match_path_extension
match_path
match_path_and_query
match_query


Variables can be inserted in target strings using ${name:transform}
where transform can be `lower`, `upper`, `trim`, `trim(a-zA-Z\t\:\\-))


## conditions 

alpha, alphanumeric, alphalower, alphaupper, guid, hex, int, only([a-zA-Z0-9_\:\,]), only(^/) len(3), length(3), length(0,3),starts_with_only(3,a-z), until(/), after(/): optional/?, equals(string), everything/**
 ends_with((.jpg|.png|.gif)), includes(), supported_image_type
ends_with(.jpg|.png|.gif), until(), after(), includes(), 

until and after specify trailing and leading characters that are part of the matching group, but are only useful if combined with `optional`.

TODO: sha256/auth stuff


respond_400_on_variable_condition_failure=true
process_image=true
pass_throgh=true
allow_pass_through=true
stop_here=true
case_sensitive=true/false (IIS/ASP.NET default to insensitive, but it's a bad default)

[routes.accepts_any]
accept_header_has_type="*/*"
add_query_value="accepts=*"
set_query_value="format=auto"

[routes.accepts_webp]
accept_header_has_type="image/webp"
add_query_value="accepts=webp"
set_query_value="format=auto"

[routes.accepts_avif]
accept_header_has_type="image/avif"
add_query_value="accepts=avif"
set_query_value="format=auto"


# Escaping characters

JSON/TOML escapes include 
\"
\\
\/ (JSON only)
\b
\f
\n
\r
\t
\u followed by four-hex-digits
\UXXXXXXXX - unicode         (U+XXXXXXXX) (TOML only?)

Test with url decoded foreign strings too

# Real-world examples

Setting a max size by folder or authentication status..

We were thinking we could save as a GUID and have some mapping, where we asked for image “St-Croix-Legend-Extreme-Rod….” and we somehow did a lookup to see what the actual GUID image name was but seems we would introduce more issues, like needing to make ImageResizer and handler and not a module to do the lookup and creating an extra database call per image. Doesn’t seem like a great solution, any ideas? Just use the descriptive name?

2. You're free to use any URL rewriting solution, the provided Config.Current.Pipeline.Rewrite event, or the included Presets plugin: http://imageresizing.net/plugins/presets

You can also add a Rewrite handler and check the HOST header if you wanted
to use subdomains instead of prefixes. Prefixes are probably better though.



## Example Accept header values
image/avif,image/webp,*/*
image/webp,*/*
*/*
image/png,image/*;q=0.8,*/*;q=0.5
image/webp,image/png,image/svg+xml,image/*;q=0.8,video/*;q=0.8,*/*;q=0.5
image/png,image/svg+xml,image/*;q=0.8,video/*;q=0.8,*/*;q=0.5
image/avif,image/webp,image/apng,image/*,*/*;q=0.8

video
video/webm,video/ogg,video/*;q=0.9,application/ogg;q=0.7,audio/*;q=0.6,*/*;q=0.5    audio/webm,audio/ogg,audio/wav,audio/*;q=0.9,application/ogg;q=0.7,video/*;q=0.6,*/*;q=0.5
*/*