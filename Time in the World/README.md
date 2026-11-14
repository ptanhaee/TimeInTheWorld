# Time in the World

A dark WPF world clock for Windows, built with .NET 8.

Run with `dotnet run`, or open the project in Rider or Visual Studio and run it.
Build a standalone app with
`dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish`,
then run `publish\Time in the World.exe` (no .NET install needed to run it).

- Analog and 12-hour digital clocks with AM/PM update every second.
- Dark styling includes both windows, native title bars, scrollbars, and tooltips.
- A custom mint clock/globe icon is included in the executable and all windows.
- Opens at up to 1600 × 1340, tall enough to show whole cards with weather, constrained to the available desktop area.
- Starts with Montreal, Vancouver, Canberra, Iran, and England.
- Use **Add clock** to search the computer's time zones or common city shortcuts,
  select a result, and optionally give it a custom name.
- Use **×** on a card to remove it. Removing all clocks is supported.
- Use **Choose weather city** at the bottom of a card, search for a city, select
  its region/country, and choose **Save city**. **Edit weather city** lets you
  change or remove it later. The city is saved with that clock; existing clock
  settings still load normally.
- The selected weather city stays visible even while weather is unavailable.
  Reopening the picker selects the saved city immediately, including offline.
  Saving a city also shows a confirmation at the bottom of the main window.
- Weather appears inside the lower part of the card: current conditions, a small
  condition icon and Celsius temperature, then today's **Morning** (6 AM–12 PM),
  **Noon** (12 PM–6 PM) and **Night** (6 PM–6 AM) averages, and a list of the
  **next 7 days** with each day's conditions, high and low. All periods and days
  use the weather city's local calendar. Each average is the mean of that
  period's hourly forecasts (including hours already past today), shown with the
  most common condition; nights crossing a daylight-saving change have 11 or 13
  hours. Daily conditions are Open-Meteo's most significant weather of the day.
  Hover a tile or day for details.
- Weather loads immediately on startup and refreshes every 30 minutes. Each
  card fetches independently; a failed request or city edit cannot hide another
  card's weather. There are no internet reachability checks. Failed cards hide
  their weather and retry at the next 30-minute update. Saving a city fetches its
  weather immediately. Requests time out after at most ten seconds, while clocks
  keep running. Loading and retry messages explain temporarily unavailable data.
  Saved cities remain available offline; searching for a new city needs internet.
- Weather and city search use [Open-Meteo](https://open-meteo.com/), with no API
  key needed for this personal app. Only city searches and selected coordinates
  are sent; the app does not request your device location. Current conditions
  come from weather models. See the [forecast](https://open-meteo.com/en/docs) and
  [city search](https://open-meteo.com/en/docs/geocoding-api) documentation.
- Clock selections are saved to `%LOCALAPPDATA%\Time in the World\clocks.json`.
- UTC offsets and daylight saving come from Windows time-zone rules. Times use
  the computer's clock; daytime means 06:00–17:59, rather than sunrise/sunset.

Requires Windows and the .NET 8 Desktop Runtime (or SDK for building).

Icon assets are in `Assets/AppIcon.svg`, `Assets/AppIcon.png`, and
`Assets/AppIcon.ico`. The ICO includes nine sizes from 16 to 256 pixels, with
simplified small sizes. `Tools/Create-AppIcon.ps1` regenerates the PNG and ICO
using the matching vector geometry, with no extra packages required.