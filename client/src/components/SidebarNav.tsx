import { NavLink } from 'react-router-dom'
import { House, TrendingUp, BarChart2, Settings } from 'lucide-react'
import { useTranslation } from 'react-i18next'

const navRoutes = [
  { to: '/', icon: House, tKey: 'dashboard', end: true },
  { to: '/insights', icon: TrendingUp, tKey: 'insights', end: false },
  { to: '/decomposition', icon: BarChart2, tKey: 'decomposition', end: false },
  { to: '/settings', icon: Settings, tKey: 'settings', end: false },
] as const

export function SidebarNav() {
  const { t } = useTranslation('common')

  return (
    <nav
      role="navigation"
      aria-label="Sidebar navigation"
      className="w-[200px] h-screen flex flex-col p-3 gap-1"
      style={{
        background: 'rgba(0,0,0,0.25)',
        WebkitBackdropFilter: 'blur(20px) saturate(180%)',
        backdropFilter: 'blur(20px) saturate(180%)',
        borderRight: '1px solid rgba(255,255,255,0.08)',
      }}
    >
      {navRoutes.map(({ to, icon: Icon, tKey, end }) => {
        const label = t(`nav.${tKey}`)
        return (
          <NavLink
            key={to}
            to={to}
            end={end}
            aria-label={label}
            className={({ isActive }) =>
              `flex flex-row items-center gap-3 px-3 py-2 min-h-[44px] transition-colors ${
                isActive ? '' : 'opacity-60 hover:opacity-80'
              }`
            }
            style={({ isActive }) =>
              isActive
                ? { background: 'rgba(255,255,255,0.12)', borderRadius: 'var(--radius-sidebar-item)' }
                : {}
            }
          >
            <Icon className="w-[22px] h-[22px] shrink-0 text-text-primary" />
            <span className="text-body-sm text-text-primary">{label}</span>
          </NavLink>
        )
      })}
    </nav>
  )
}
