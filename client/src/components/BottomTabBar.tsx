import { NavLink } from 'react-router-dom'
import { House, TrendingUp, BarChart2, Settings } from 'lucide-react'
import { useTranslation } from 'react-i18next'

const tabRoutes = [
  { to: '/', icon: House, tKey: 'dashboard', end: true },
  { to: '/insights', icon: TrendingUp, tKey: 'insights', end: false },
  { to: '/decomposition', icon: BarChart2, tKey: 'decomposition', end: false },
  { to: '/settings', icon: Settings, tKey: 'settings', end: false },
] as const

export function BottomTabBar() {
  const { t } = useTranslation('common')

  return (
    <nav
      role="navigation"
      aria-label="Bottom navigation"
      className="fixed bottom-0 left-0 right-0 h-[72px] flex items-center"
      style={{
        background: 'rgba(10,15,25,0.75)',
        WebkitBackdropFilter: 'blur(20px) saturate(180%)',
        backdropFilter: 'blur(20px) saturate(180%)',
        borderTop: '1px solid rgba(255,255,255,0.10)',
      }}
    >
      {tabRoutes.map(({ to, icon: Icon, tKey, end }) => {
        const label = t(`nav.${tKey}`)
        return (
          <NavLink
            key={to}
            to={to}
            end={end}
            aria-label={label}
            className={({ isActive }) =>
              `flex flex-col items-center justify-center flex-1 min-h-[44px] min-w-[44px] gap-1 ${
                isActive
                  ? 'opacity-100 text-text-primary'
                  : 'opacity-40 text-text-tertiary'
              }`
            }
          >
            <Icon className="w-[22px] h-[22px]" />
            <span className="text-micro uppercase">{label}</span>
          </NavLink>
        )
      })}
    </nav>
  )
}
