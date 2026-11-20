# Member entry, historical offices, and languages

New member registration selects the active **Regular** category when its options load. It preserves explicit choices and restored draft categories. If Regular is missing or inactive, the form still asks for a category rather than guessing another one. Existing memberships are unchanged.

Every organizational unit's details now include **Past office bearers**, with **Past presidents**, all offices, and service-year filters. The International page exposes this section directly; National, Region, and Club details expose it within the unit information. Club workspaces and the structure intelligence detail panel show the same records.

Completed and expired effective appointments appear automatically. Years are interpreted in the unit's time zone and appointment end dates remain exclusive. Proposed/rejected appointments are not service history. Older paper records can be entered with a name, office, president classification, start/end years, and notes. They are independent of memberships, users, and access assignments. Edits are version-checked; archiving retains the stored record. Creation, updates and archives produce audit entries. Reads require `leadership.view`; changes require `leadership.manage` and the same tenant/unit scope checks as existing leadership tools.

Apply migration `HistoricalOfficeServiceRecords` before starting this version. It adds one table and does not rewrite appointments or role assignments. Back up the database before upgrading.

English remains the default. The language selector offers English and Telugu. Anonymous selection uses a preference cookie; authenticated selection is saved in the user's profile. The account language is restored after login and reload. Shared navigation, authentication, member entry, account preferences and the new historical-office tools have Telugu messages. Other specialized screens fall back to their existing English labels. No browser-language detection changes the default. Existing Hindi content preferences remain compatible; Hindi interface translation is not advertised.

Run `tests/office_service_records.py` against a migrated disposable copy whose name starts with `clubs_ui_history_te_`. Set `CLUBS_TEST_URL` and `CLUBS_TEST_DATABASE` explicitly. Browser verification additionally covers Regular's default and manual override, Telugu switching/persistence, and the unit's embedded history editor.
