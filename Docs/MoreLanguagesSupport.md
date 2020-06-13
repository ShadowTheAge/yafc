# YAFC support for more languages

YAFC language support is exprimental. If your language is missing, that is probably because of one of two reasons:

- It has less than 90% support in official Factorio translation
- It uses non-european glyphs (such as Chinese or Japanese languages)

You can enable support for your language using this method:
- Navigate to `yafc.config` file located at `%localappdata%\YAFC` (`C:\Users\username\AppData\Local\YAFC`). Open it with the text editor.
- find `language` section and replace the value with your language code. Here are examples of language codes:
    - Chinese (Simplified): `zh-CN`
	- Chinese (Traditional): `zh-TW`
	- Korean: `ko`
	- Japanese: `ja`
	- Hebrew: `he`
	- Else: Look into `Factorio/data/base/locale` folder and find folder with your language.
- If your language have non-european glyphs, you also need to replace fonts: `YAFC/Data/Roboto-Light.ttf` and `Roboto-Regular.ttf` with any fonts that support your language glyphs.