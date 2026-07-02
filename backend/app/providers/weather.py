import logging

import httpx

from app.core.config import Settings
from app.models.external_info import WeatherCurrent, WeatherCurrentResponse, WeatherLocation


logger = logging.getLogger(__name__)


class WeatherProviderError(RuntimeError):
    pass


class WeatherProvider:
    name = "open-meteo"

    def __init__(
        self,
        settings: Settings,
        *,
        transport: httpx.AsyncBaseTransport | None = None,
    ):
        self.settings = settings
        self._geocoding_client = httpx.AsyncClient(
            base_url=settings.open_meteo_geocoding_base_url,
            timeout=8.0,
            transport=transport,
        )
        self._forecast_client = httpx.AsyncClient(
            base_url=settings.open_meteo_forecast_base_url,
            timeout=8.0,
            transport=transport,
        )

    async def current_weather(self, location: str) -> WeatherCurrentResponse:
        query = location.strip()
        if not query:
            raise WeatherProviderError("Location is required.")

        place = await self._geocode(query)
        forecast = await self._current_forecast(place)
        return WeatherCurrentResponse(
            provider=self.name,
            location=place,
            current=forecast,
        )

    async def _geocode(self, location: str) -> WeatherLocation:
        try:
            response = await self._geocoding_client.get(
                "/search",
                params={
                    "name": location,
                    "count": 1,
                    "language": "ja",
                    "format": "json",
                },
            )
            response.raise_for_status()
        except httpx.HTTPError as exc:
            logger.info("Open-Meteo geocoding failed: %s", exc)
            raise WeatherProviderError("Weather location lookup failed.") from exc

        payload = response.json()
        results = payload.get("results") if isinstance(payload, dict) else None
        if not results:
            raise WeatherProviderError(f"Location not found: {location}")

        first = results[0]
        return WeatherLocation(
            name=str(first.get("name") or location),
            country=str(first.get("country") or ""),
            admin1=str(first.get("admin1") or ""),
            latitude=float(first["latitude"]),
            longitude=float(first["longitude"]),
            timezone=str(first.get("timezone") or ""),
        )

    async def _current_forecast(self, location: WeatherLocation) -> WeatherCurrent:
        try:
            response = await self._forecast_client.get(
                "/forecast",
                params={
                    "latitude": location.latitude,
                    "longitude": location.longitude,
                    "current": ",".join(
                        (
                            "temperature_2m",
                            "relative_humidity_2m",
                            "apparent_temperature",
                            "precipitation",
                            "weather_code",
                            "wind_speed_10m",
                        )
                    ),
                    "timezone": "auto",
                },
            )
            response.raise_for_status()
        except httpx.HTTPError as exc:
            logger.info("Open-Meteo forecast failed: %s", exc)
            raise WeatherProviderError("Weather forecast request failed.") from exc

        payload = response.json()
        current = payload.get("current", {}) if isinstance(payload, dict) else {}
        units = payload.get("current_units", {}) if isinstance(payload, dict) else {}
        return WeatherCurrent(
            time=str(current.get("time") or ""),
            temperature=current.get("temperature_2m"),
            temperature_unit=str(units.get("temperature_2m") or ""),
            relative_humidity=current.get("relative_humidity_2m"),
            relative_humidity_unit=str(units.get("relative_humidity_2m") or ""),
            apparent_temperature=current.get("apparent_temperature"),
            apparent_temperature_unit=str(units.get("apparent_temperature") or ""),
            precipitation=current.get("precipitation"),
            precipitation_unit=str(units.get("precipitation") or ""),
            weather_code=current.get("weather_code"),
            wind_speed=current.get("wind_speed_10m"),
            wind_speed_unit=str(units.get("wind_speed_10m") or ""),
        )
