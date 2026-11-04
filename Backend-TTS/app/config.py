import os
from pathlib import Path
from dotenv import load_dotenv

BASE_DIR = Path(__file__).resolve().parent.parent
load_dotenv(BASE_DIR / ".env")

class Settings:
    """Configuración centralizada del microservicio TTS."""
    TTS_VOICE: str = os.getenv("TTS_VOICE", "es-MX-JorgeNeural")
    TTS_RATE: str = os.getenv("TTS_RATE", "+5%")
    TTS_VOLUME: str = os.getenv("TTS_VOLUME", "+0%")
    TTS_PITCH: str = os.getenv("TTS_PITCH", "-5Hz")
    TTS_MAX_TEXT_LENGTH: int = int(os.getenv("TTS_MAX_TEXT_LENGTH", "500"))
    SERVER_HOST: str = os.getenv("SERVER_HOST", "0.0.0.0")
    SERVER_PORT: int = int(os.getenv("SERVER_PORT", "8000"))
    LRU_CACHE_CAPACITY: int = int(os.getenv("LRU_CACHE_CAPACITY", "128"))

settings = Settings()
