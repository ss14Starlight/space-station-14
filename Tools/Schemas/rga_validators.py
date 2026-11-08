from yamale.validators import Validator
import validators

class License(Validator):
    tag = "license"
    licenses = [
        "CC-BY-1.0",
        "CC-BY-2.0",
        "CC-BY-2.5",
        "CC-BY-3.0",
        "CC-BY-4.0",
        "CC-BY-SA-1.0",
        "CC-BY-SA-2.0",
        "CC-BY-SA-2.5",
        "CC-BY-SA-3.0",
        "CC-BY-SA-4.0",
        "CC-BY-NC-1.0",
        "CC-BY-NC-2.0",
        "CC-BY-NC-2.5",
        "CC-BY-NC-3.0",
        "CC-BY-NC-4.0",
        "CC-BY-ND-1.0",
        "CC-BY-ND-2.0",
        "CC-BY-ND-2.5",
        "CC-BY-ND-3.0",
        "CC-BY-ND-4.0",
        "CC-BY-NC-SA-1.0",
        "CC-BY-NC-SA-2.0",
        "CC-BY-NC-SA-2.5",
        "CC-BY-NC-SA-3.0",
        "CC-BY-NC-SA-4.0",
        "CC-BY-NC-ND-1.0",
        "CC-BY-NC-ND-2.0",
        "CC-BY-NC-ND-2.5",
        "CC-BY-NC-ND-3.0",
        "CC-BY-NC-ND-4.0",
        "CC0-1.0",
        "MIT",
        "Apache-2.0",
        "GPL-2.0",
        "GPL-3.0",
        "LGPL-2.1",
        "LGPL-3.0",
        "AGPL-3.0",
        "BSD-2-Clause",
        "BSD-3-Clause",
        "ISC",
        "MPL-2.0",
        "Unlicense",
        "Artistic-2.0",
        "BSL-1.0",
        "Zlib",
        "EPL-2.0",
        "CDDL-1.0",
        "MS-PL",
        "OSL-3.0",
        "OFL-1.0",
        "OFL-1.1",
        "Custom",
        ]

    def _is_valid(self, value):
        return value in self.licenses

class Url(Validator):
    tag = "url"

    def _is_valid(self, value):
        # Source field is required to ensure its not neglected, but there may be no applicable URL
        return (value == "NA") or validators.url(value)
