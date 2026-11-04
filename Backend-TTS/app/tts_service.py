import io
import asyncio
from collections import OrderedDict
from typing import Optional, Tuple
import edge_tts
from app.config import settings

class TTSService:
    """
    Servicio de síntesis de voz en memoria utilizando edge-tts con caché LRU.
    Garantiza CERO escrituras en disco (Zero Disk I/O) y respuesta inmediata
    para frases repetidas o de combate frecuente.
    """

    def __init__(self, cache_capacity: int = 128):
        self._cache_capacity = cache_capacity
        self._cache: OrderedDict[Tuple[str, str, str, str, str], bytes] = OrderedDict()
        self._lock = asyncio.Lock()
        self._total_requests = 0
        self._cache_hits = 0

    async def generate_audio_bytes(
        self,
        text: str,
        voice: Optional[str] = None,
        rate: Optional[str] = None,
        volume: Optional[str] = None,
        pitch: Optional[str] = None
    ) -> bytes:
        """
        Genera el audio MP3 en memoria RAM a partir de texto o lo recupera de la caché.
        """
        final_voice = voice.strip() if voice and voice.strip() else settings.TTS_VOICE
        final_rate = rate.strip() if rate and rate.strip() else settings.TTS_RATE
        final_volume = volume.strip() if volume and volume.strip() else settings.TTS_VOLUME
        final_pitch = pitch.strip() if pitch and pitch.strip() else settings.TTS_PITCH

        cache_key = (text, final_voice, final_rate, final_volume, final_pitch)

        async with self._lock:
            self._total_requests += 1
            if cache_key in self._cache:
                self._cache_hits += 1
                self._cache.move_to_end(cache_key)
                return self._cache[cache_key]

        # Generar audio streaming directamente a memoria (io.BytesIO)
        buffer = io.BytesIO()
        communicate = edge_tts.Communicate(
            text=text,
            voice=final_voice,
            rate=final_rate,
            volume=final_volume,
            pitch=final_pitch
        )

        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                buffer.write(chunk["data"])

        audio_bytes = buffer.getvalue()
        if not audio_bytes:
            raise RuntimeError("El motor edge-tts finalizó sin generar ningún fragmento de audio.")

        # Guardar en caché LRU
        async with self._lock:
            self._cache[cache_key] = audio_bytes
            self._cache.move_to_end(cache_key)
            if len(self._cache) > self._cache_capacity:
                self._cache.popitem(last=False)

        return audio_bytes

    async def get_cached_count(self) -> int:
        async with self._lock:
            return len(self._cache)

    async def clear_cache(self) -> None:
        async with self._lock:
            self._cache.clear()

tts_service = TTSService(cache_capacity=settings.LRU_CACHE_CAPACITY)
