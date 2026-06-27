import httpx
import pytest
from fastapi.testclient import TestClient

from app.core.config import Settings
from app.main import app
from app.models.external_info import WeatherCurrentResponse
from app.providers.weather import WeatherProvider, WeatherProviderError


@pytest.mark.anyio
async def test_weather_provider_geocodes_location_and_fetches_current_weather() -> None:
    requests: list[httpx.Request] = []

    def handler(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        if request.url.host == "geo.example.test":
            return httpx.Response(
                200,
                json={
                    "results": [
                        {
                            "name": "Tokyo",
                            "country": "Japan",
                            "admin1": "Tokyo",
                            "latitude": 35.6895,
                            "longitude": 139.6917,
                            "timezone": "Asia/Tokyo",
                        }
                    ]
                },
            )
        return httpx.Response(
            200,
            json={
                "current": {
                    "time": "2026-06-24T15:00",
                    "temperature_2m": 28.4,
                    "relative_humidity_2m": 61,
                    "apparent_temperature": 30.1,
                    "precipitation": 0.0,
                    "weather_code": 2,
                    "wind_speed_10m": 7.5,
                },
                "current_units": {
                    "temperature_2m": "°C",
                    "relative_humidity_2m": "%",
                    "apparent_temperature": "°C",
                    "precipitation": "mm",
                    "wind_speed_10m": "km/h",
                },
            },
        )

    provider = WeatherProvider(
        Settings(
            open_meteo_geocoding_base_url="https://geo.example.test/v1",
            open_meteo_forecast_base_url="https://forecast.example.test/v1",
        ),
        transport=httpx.MockTransport(handler),
    )

    result = await provider.current_weather("東京")

    assert result.provider == "open-meteo"
    assert result.location.name == "Tokyo"
    assert result.location.country == "Japan"
    assert result.current.temperature == 28.4
    assert result.current.weather_code == 2
    assert requests[0].url.path == "/v1/search"
    assert requests[0].url.params["name"] == "東京"
    assert requests[1].url.path == "/v1/forecast"
    assert requests[1].url.params["latitude"] == "35.6895"


@pytest.mark.anyio
async def test_weather_provider_reports_unknown_location() -> None:
    provider = WeatherProvider(
        Settings(open_meteo_geocoding_base_url="https://geo.example.test/v1"),
        transport=httpx.MockTransport(lambda _: httpx.Response(200, json={"results": []})),
    )

    with pytest.raises(WeatherProviderError, match="Location not found"):
        await provider.current_weather("not-a-place")


def test_weather_current_route_returns_provider_response() -> None:
    app.dependency_overrides.clear()
    from app.api.routes import get_weather_provider

    class FakeWeatherProvider:
        async def current_weather(self, location: str) -> WeatherCurrentResponse:
            assert location == "Tokyo"
            return WeatherCurrentResponse(
                provider="open-meteo",
                location={
                    "name": "Tokyo",
                    "country": "Japan",
                    "admin1": "Tokyo",
                    "latitude": 35.6895,
                    "longitude": 139.6917,
                    "timezone": "Asia/Tokyo",
                },
                current={
                    "time": "2026-06-24T15:00",
                    "temperature": 28.4,
                    "temperature_unit": "°C",
                    "relative_humidity": 61,
                    "relative_humidity_unit": "%",
                    "apparent_temperature": 30.1,
                    "apparent_temperature_unit": "°C",
                    "precipitation": 0.0,
                    "precipitation_unit": "mm",
                    "weather_code": 2,
                    "wind_speed": 7.5,
                    "wind_speed_unit": "km/h",
                },
            )

    app.dependency_overrides[get_weather_provider] = lambda: FakeWeatherProvider()
    try:
        response = TestClient(app).get("/external/weather/current", params={"location": "Tokyo"})
    finally:
        app.dependency_overrides.clear()

    assert response.status_code == 200
    assert response.json()["current"]["temperature"] == 28.4
