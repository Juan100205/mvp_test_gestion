/** Curvas de nivel generadas: evocan el territorio y las regiones donde trabajan los tutores. */
const lineas = Array.from({ length: 18 }, (_, i) => {
  const base = 40 + i * 34
  const puntos = Array.from({ length: 9 }, (_, j) => {
    const x = j * 75
    const y = base + Math.sin(j * 0.9 + i * 0.45) * (18 + i * 1.6) + Math.cos(j * 0.35 - i * 0.2) * 22
    return [x, y] as const
  })
  return puntos.reduce((d, [x, y], j) => {
    if (j === 0) return `M${x},${y.toFixed(1)}`
    const [px, py] = puntos[j - 1]!
    const cx = (px + x) / 2
    return `${d} C${cx},${py.toFixed(1)} ${cx},${y.toFixed(1)} ${x},${y.toFixed(1)}`
  }, '')
})

export function CurvasDeNivel({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 600 640" preserveAspectRatio="xMidYMid slice" aria-hidden className={className}>
      {lineas.map((d, i) => (
        <path
          key={i}
          d={d}
          fill="none"
          stroke={i === 11 ? 'var(--color-terracota)' : 'currentColor'}
          strokeWidth={i === 11 ? 1.6 : 0.8}
          opacity={i === 11 ? 1 : 0.22 + (i % 4) * 0.06}
        />
      ))}
    </svg>
  )
}
