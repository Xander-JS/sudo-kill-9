FROM python:3.11-slim

WORKDIR /app

# Crear usuario sin privilegios para mayor seguridad
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copiar dependencias e instalarlas
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copiar código fuente
COPY app/ ./app/

# Usar usuario no root
USER appuser

EXPOSE 8000

ENV PYTHONUNBUFFERED=1

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
