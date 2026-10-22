/**
 * Extrai uma mensagem legível de um erro do axios, cobrindo os formatos da API:
 * ProblemDetails (detail/title) e ValidationProblem (errors).
 */
export function apiErrorMessage (error: any, fallback = 'Ocorreu um erro inesperado.'): string {
  const data = error?.response?.data

  if (data?.detail) {
    return data.detail
  }

  if (data?.errors) {
    const messages = Object.values(data.errors).flat().filter(Boolean) as string[]
    if (messages.length > 0) {
      return messages.join(' ')
    }
  }

  if (typeof data?.title === 'string') {
    return data.title
  }

  return fallback
}
