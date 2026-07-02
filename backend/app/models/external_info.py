from pydantic import BaseModel


class WeatherLocation(BaseModel):
    name: str
    country: str = ""
    admin1: str = ""
    latitude: float
    longitude: float
    timezone: str = ""


class WeatherCurrent(BaseModel):
    time: str
    temperature: float | None = None
    temperature_unit: str = ""
    relative_humidity: int | None = None
    relative_humidity_unit: str = ""
    apparent_temperature: float | None = None
    apparent_temperature_unit: str = ""
    precipitation: float | None = None
    precipitation_unit: str = ""
    weather_code: int | None = None
    wind_speed: float | None = None
    wind_speed_unit: str = ""


class WeatherCurrentResponse(BaseModel):
    provider: str
    location: WeatherLocation
    current: WeatherCurrent
