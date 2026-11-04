from typing import Optional, List
from pydantic import BaseModel, Field, field_validator

class AudioRequest(BaseModel):
    text: str = Field(..., description="Texto que dirá Windows Boss", min_length=1)
    voice: Optional[str] = Field(None, description="Voz de Edge-TTS opcional (ej: es-MX-JorgeNeural)")
    rate: Optional[str] = Field(None, description="Modificador de velocidad (ej: +5%, -10%)")
    volume: Optional[str] = Field(None, description="Modificador de volumen (ej: +0%, -20%)")
    pitch: Optional[str] = Field(None, description="Modificador de tono (ej: -5Hz, +10Hz)")

    @field_validator("text")
    @classmethod
    def sanitize_text(cls, v: str) -> str:
        cleaned = v.strip()
        if not cleaned:
            raise ValueError("El texto a sintetizar no puede estar vacío.")
        return cleaned

class HealthResponse(BaseModel):
    status: str
    service: str
    default_voice: str
    cached_audios_count: int

class VoiceInfo(BaseModel):
    id: str
    name: str
    gender: str
    language: str
    description: str
