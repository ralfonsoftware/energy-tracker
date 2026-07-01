import { useState } from 'react'
import { Zap } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Sheet, SheetTrigger } from '@/components/ui/sheet'
import { EnterReadingSheet } from './EnterReadingSheet'

type Props = { flatId: string | undefined; lastKwhValue: number | null; onSubmitSuccess?: () => void }

export function EnterReadingCta({ flatId, lastKwhValue, onSubmitSuccess }: Props) {
  const { t } = useTranslation('readings')
  const [open, setOpen] = useState(false)

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <button
          type="button"
          className="flex w-full items-center justify-center gap-2 rounded-pill border-[1.5px] border-white/40 bg-white/[0.10] px-6 py-4 text-body text-text-primary backdrop-blur-[20px] backdrop-saturate-[1.8] md:h-11 md:w-11 md:rounded-[14px] md:border md:gap-0 md:px-0 md:py-0"
          aria-label={t('sheet.ctaLabel')}
        >
          <Zap size={20} className="hidden text-text-primary md:block" />
          <span className="md:hidden">{t('sheet.ctaLabel')}</span>
        </button>
      </SheetTrigger>
      <EnterReadingSheet
        flatId={flatId}
        lastKwhValue={lastKwhValue}
        open={open}
        onOpenChange={setOpen}
        onSubmitSuccess={onSubmitSuccess}
      />
    </Sheet>
  )
}
