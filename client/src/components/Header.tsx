import { FlatSwitcher } from './FlatSwitcher'

export function Header() {
  return (
    <header
      role="banner"
      className="flex items-center px-4 py-3"
      style={{
        background: 'rgba(255,255,255,0.07)',
        border: '1px solid rgba(255,255,255,0.12)',
        backdropFilter: 'blur(20px)',
      }}
    >
      <FlatSwitcher />
    </header>
  )
}
