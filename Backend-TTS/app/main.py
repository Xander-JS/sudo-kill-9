from typing import List
from fastapi import FastAPI, HTTPException, Response, status
from fastapi.middleware.cors import CORSMiddleware
from app.config import settings
from app.schemas import AudioRequest, HealthResponse, VoiceInfo
from app.tts_service import tts_service

app = FastAPI(
    title="Sudo-Kill-9 Windows Boss Voice Microservice",
    description="Microservicio local de síntesis de voz (TTS) para el villano Windows en Sudo-Kill-9",
    version="1.0.0"
)

# Habilitar CORS para permitir llamadas directas desde Unity o herramientas locales
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

RECOMENDED_VOICES: List[VoiceInfo] = [
    VoiceInfo(
        id="es-MX-JorgeNeural",
        name="Jorge (México)",
        gender="Masculino",
        language="es-MX",
        description="Voz masculina autoritaria y técnica, ideal para la personificación de Windows en español latino."
    ),
    VoiceInfo(
        id="es-ES-AlvaroNeural",
        name="Álvaro (España)",
        gender="Masculino",
        language="es-ES",
        description="Voz masculina formal y seria en español peninsular."
    ),
    VoiceInfo(
        id="en-US-ChristopherNeural",
        name="Christopher (EE.UU.)",
        gender="Masculino",
        language="en-US",
        description="Voz ejecutiva y autoritaria en inglés estadounidense."
    ),
    VoiceInfo(
        id="en-US-GuyNeural",
        name="Guy (EE.UU.)",
        gender="Masculino",
        language="en-US",
        description="Voz masculina enérgica y confiada en inglés."
    )
]

@app.get("/health", response_model=HealthResponse, tags=["Monitoreo"])
async def health_check():
    """Verifica el estado del servicio y el tamaño de la caché de audio."""
    cached_count = await tts_service.get_cached_count()
    return HealthResponse(
        status="ok",
        service="Sudo-Kill-9 Windows Boss TTS Microservice",
        default_voice=settings.TTS_VOICE,
        cached_audios_count=cached_count
    )

@app.get("/voices", response_model=List[VoiceInfo], tags=["Configuración"])
async def list_voices():
    """Lista las voces predeterminadas recomendadas para el jefe Windows."""
    return RECOMENDED_VOICES

@app.post(
    "/generate_audio",
    response_class=Response,
    responses={
        200: {
            "content": {"audio/mpeg": {}},
            "description": "Stream de audio MP3 generado en memoria"
        },
        400: {"description": "Petición inválida o texto demasiado largo"},
        500: {"description": "Error interno durante la síntesis con edge-tts"}
    },
    tags=["Síntesis"]
)
async def generate_audio(request: AudioRequest):
    """
    Sintetiza el texto solicitado a un flujo MP3 en memoria RAM y lo devuelve a Unity.
    Utiliza caché LRU para responder con 0 ms de latencia adicional a frases repetidas.
    """
    if len(request.text) > settings.TTS_MAX_TEXT_LENGTH:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"El texto supera el límite permitido de {settings.TTS_MAX_TEXT_LENGTH} caracteres."
        )

    try:
        audio_bytes = await tts_service.generate_audio_bytes(
            text=request.text,
            voice=request.voice,
            rate=request.rate,
            volume=request.volume,
            pitch=request.pitch
        )

        return Response(
            content=audio_bytes,
            media_type="audio/mpeg",
            headers={
                "Content-Disposition": "inline; filename=boss_voice.mp3",
                "Cache-Control": "public, max-age=3600"
            }
        )
    except Exception as exc:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Fallo en la síntesis de voz: {str(exc)}"
        )

@app.post("/clear_cache", tags=["Mantenimiento"])
async def clear_cache():
    """Limpia la caché de audios en memoria RAM."""
    await tts_service.clear_cache()
    return {"message": "Caché de audio limpiada exitosamente."}
