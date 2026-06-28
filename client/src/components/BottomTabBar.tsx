import { NavLink } from 'react-router-dom'
import { House, TrendingUp, BarChart2, Settings } from 'lucide-react'

const tabs = [
  { to: '/', label: 'Dashboard', Icon: House, end: true },
  { to: '/insights', label: 'Insights', Icon: TrendingUp, end: false },
  { to: '/decomposition', label: 'Decomposition', Icon: BarChart2, end: false },
  { to: '/settings', label: 'Settings', Icon: Settings, end: false },
]

export function BottomTabBar() {
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
      {tabs.map(({ to, label, Icon, end }) => (
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
      ))}
    </nav>
  )
}
