import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { ScrollArea } from '@/components/ui/scroll-area';
import { cn } from '@/lib/utils';
import { Input } from '@/components/ui/input';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { useState } from 'react';
import {
    Home,
    Car,
    Utensils,
    ShoppingBag,
    CreditCard,
    Banknote,
    Zap,
    Wifi,
    Phone,
    Tv,
    Music,
    Film,
    Gamepad2,
    Book,
    GraduationCap,
    Briefcase,
    Hammer,
    Wrench,
    Gift,
    Heart,
    Plane,
    Train,
    Bus,
    Fuel,
    Coffee,
    Beer,
    Wine,
    Cigarette,
    Pill,
    Stethoscope,
    Scissors,
    Shirt,
    Watch,
    Smartphone,
    Laptop,
    Camera,
    Headphones,
    Speaker,
    Map,
    Globe,
    Sun,
    Moon,
    Cloud,
    Umbrella,
    Droplets,
    Flame,
    Leaf,
    Flower,
    Trees,
    PawPrint,
    Dog,
    Cat,
    Baby,
    User,
    Users,
    Smile,
    LucideIcon,
    ShoppingCart,
    Landmark,
    // Additional icons for more categories
    Wallet,
    PiggyBank,
    TrendingUp,
    DollarSign,
    BadgePercent,
    Receipt,
    FileText,
    Calculator,
    Building,
    Building2,
    Sparkles,
    Sparkle,
    Scissors as Haircut,
    Eye,
    Pipette,
    Armchair,
    Sofa,
    Lamp,
    LampDesk,
    TreePine,
    Sprout,
    Apple,
    UtensilsCrossed,
    ChefHat,
    Pizza,
    IceCream,
    Cake,
    Candy,
    Drama,
    Theater,
    Tickets,
    PartyPopper,
    Dice1,
    Dices,
    Dice5,
    Dumbbell,
    Trophy,
    Medal,
    Bike,
    Footprints,
    Ship,
    Sailboat,
    Hotel,
    BedDouble,
    Bed,
    Key,
    MapPin,
    Compass,
    Palmtree,
    Mountain,
    Languages,
    Guitar,
    Piano,
    Brush,
    Palette,
    Paintbrush,
    Package,
    PackageOpen,
    Boxes,
    Archive,
    Shirt as TShirt,
    Footprints as Shoes,
    ShowerHead,
    Sparkles as Beauty,
    Gem,
    Crown,
    Glasses,
    Lightbulb,
    Plug,
    Cpu,
    HardDrive,
    Monitor,
    MonitorSmartphone,
    Tablet,
    Printer,
    Keyboard,
    Mouse,
    Gamepad,
    Wand,
    Wand2,
    CircleDollarSign,
    CoinsIcon as Coins,
    HandCoins,
    CandlestickChart,
    LineChart,
    CirclePercent,
    Percent,
    ReceiptText,
    ClipboardList,
    FileSpreadsheet,
    Newspaper,
    Code,
    Binary,
    Server,
    Database,
    ShieldCheck,
    Lock,
    Unlock,
    UserCheck,
    UsersRound,
    Baby as BabyIcon,
    Cake as Birthday,
    Trees as Forest,
    Warehouse,
    Store,
    ShoppingBasket,
    CircleParking,
    ParkingCircle,
    Ticket,
    BadgeCheck,
    Award,
    BadgeDollarSign,
    ArrowDownUp,
    ArrowUpDown,
    ArrowRightLeft,
    Repeat,
    BadgeHelp,
    CircleHelp,
    HelpCircle,
    Settings,
    Wrench as Tool,
    Cog,
    Plug2,
    Factory,
    Drill,
    Construction,
    PaintBucket,
    Ruler,
    Triangle,
    Square,
    Circle,
    Goal,
    Target,
    Scale,
    ClipboardCheck,
    Check,
    X,
    AlertCircle,
    Info,
    BellRing,
    Calendar,
    CalendarDays,
    Clock,
    Timer,
    Hourglass,
    Flag,
    Milestone,
    Tag,
    Tags,
    Bookmark,
    Star,
    StarHalf,
    ThumbsUp,
    ThumbsDown,
    MessageCircle,
    Mail,
    Send,
    Share,
    Link,
    ExternalLink,
    Download,
    Upload,
    Plus,
    Minus,
    Edit,
    Trash,
    Save,
} from 'lucide-react';

export const ICON_MAP = {
    // Existing icons
    home: Home,
    car: Car,
    utensils: Utensils,
    shopping_bag: ShoppingBag,
    shopping_cart: ShoppingCart,
    credit_card: CreditCard,
    banknote: Banknote,
    landmark: Landmark,
    zap: Zap,
    wifi: Wifi,
    phone: Phone,
    tv: Tv,
    music: Music,
    film: Film,
    gamepad: Gamepad2,
    book: Book,
    graduation_cap: GraduationCap,
    briefcase: Briefcase,
    hammer: Hammer,
    wrench: Wrench,
    gift: Gift,
    heart: Heart,
    plane: Plane,
    train: Train,
    bus: Bus,
    fuel: Fuel,
    coffee: Coffee,
    beer: Beer,
    wine: Wine,
    cigarette: Cigarette,
    pill: Pill,
    stethoscope: Stethoscope,
    scissors: Scissors,
    shirt: Shirt,
    watch: Watch,
    smartphone: Smartphone,
    laptop: Laptop,
    camera: Camera,
    headphones: Headphones,
    speaker: Speaker,
    map: Map,
    globe: Globe,
    sun: Sun,
    moon: Moon,
    cloud: Cloud,
    umbrella: Umbrella,
    droplets: Droplets,
    flame: Flame,
    leaf: Leaf,
    flower: Flower,
    tree: Trees,
    paw_print: PawPrint,
    dog: Dog,
    cat: Cat,
    baby: Baby,
    user: User,
    users: Users,
    smile: Smile,

    // Financial & Banking
    wallet: Wallet,
    piggy_bank: PiggyBank,
    trending_up: TrendingUp,
    dollar_sign: DollarSign,
    badge_percent: BadgePercent,
    receipt: Receipt,
    receipt_text: ReceiptText,
    file_text: FileText,
    calculator: Calculator,
    building: Building,
    building2: Building2,
    circle_dollar_sign: CircleDollarSign,
    coins: Coins,
    hand_coins: HandCoins,
    candlestick_chart: CandlestickChart,
    line_chart: LineChart,
    circle_percent: CirclePercent,
    percent: Percent,
    badge_dollar_sign: BadgeDollarSign,

    // Beauty & Personal Care
    sparkles: Sparkles,
    sparkle: Sparkle,
    haircut: Haircut,
    eye: Eye,
    pipette: Pipette,
    shower_head: ShowerHead,
    beauty: Beauty,
    gem: Gem,
    crown: Crown,
    glasses: Glasses,

    // Home & Furniture
    armchair: Armchair,
    sofa: Sofa,
    lamp: Lamp,
    lamp_desk: LampDesk,
    lightbulb: Lightbulb,
    plug: Plug,
    plug2: Plug2,

    // Garden & Nature
    tree_pine: TreePine,
    sprout: Sprout,
    palmtree: Palmtree,
    mountain: Mountain,
    forest: Forest,

    // Food & Dining
    apple: Apple,
    utensils_crossed: UtensilsCrossed,
    chef_hat: ChefHat,
    pizza: Pizza,
    ice_cream: IceCream,
    cake: Cake,
    candy: Candy,

    // Entertainment & Activities
    drama: Drama,
    theater: Theater,
    tickets: Tickets,
    ticket: Ticket,
    party_popper: PartyPopper,
    dice1: Dice1,
    dices: Dices,
    dice5: Dice5,
    gamepad2: Gamepad,

    // Sports & Fitness
    dumbbell: Dumbbell,
    trophy: Trophy,
    medal: Medal,
    award: Award,
    goal: Goal,
    target: Target,

    // Transportation
    bike: Bike,
    footprints: Footprints,
    ship: Ship,
    sailboat: Sailboat,
    circle_parking: CircleParking,
    parking_circle: ParkingCircle,

    // Travel & Accommodation
    hotel: Hotel,
    bed_double: BedDouble,
    bed: Bed,
    key: Key,
    map_pin: MapPin,
    compass: Compass,

    // Education & Hobbies
    languages: Languages,
    guitar: Guitar,
    piano: Piano,
    brush: Brush,
    palette: Palette,
    paintbrush: Paintbrush,

    // Shopping & Packages
    package: Package,
    package_open: PackageOpen,
    boxes: Boxes,
    archive: Archive,
    warehouse: Warehouse,
    store: Store,
    shopping_basket: ShoppingBasket,

    // Clothing
    t_shirt: TShirt,
    shoes: Shoes,

    // Electronics & Technology
    cpu: Cpu,
    hard_drive: HardDrive,
    monitor: Monitor,
    monitor_smartphone: MonitorSmartphone,
    tablet: Tablet,
    printer: Printer,
    keyboard: Keyboard,
    mouse: Mouse,
    code: Code,
    binary: Binary,
    server: Server,
    database: Database,

    // Business & Office
    clipboard_list: ClipboardList,
    file_spreadsheet: FileSpreadsheet,
    newspaper: Newspaper,
    clipboard_check: ClipboardCheck,

    // Security & Access
    shield_check: ShieldCheck,
    lock: Lock,
    unlock: Unlock,

    // People & Family
    user_check: UserCheck,
    users_round: UsersRound,
    baby_icon: BabyIcon,

    // Special Occasions
    birthday: Birthday,

    // Tools & Maintenance
    wand: Wand,
    wand2: Wand2,
    tool: Tool,
    cog: Cog,
    settings: Settings,
    factory: Factory,
    drill: Drill,
    construction: Construction,
    paint_bucket: PaintBucket,
    ruler: Ruler,
    scale: Scale,

    // Transfers & Transactions
    arrow_down_up: ArrowDownUp,
    arrow_up_down: ArrowUpDown,
    arrow_right_left: ArrowRightLeft,
    repeat: Repeat,

    // Miscellaneous
    badge_help: BadgeHelp,
    circle_help: CircleHelp,
    help_circle: HelpCircle,
    badge_check: BadgeCheck,
    triangle: Triangle,
    square: Square,
    circle: Circle,
    check: Check,
    x: X,
    alert_circle: AlertCircle,
    info: Info,
    bell_ring: BellRing,

    // Time & Calendar
    calendar: Calendar,
    calendar_days: CalendarDays,
    clock: Clock,
    timer: Timer,
    hourglass: Hourglass,

    // Organization
    flag: Flag,
    milestone: Milestone,
    tag: Tag,
    tags: Tags,
    bookmark: Bookmark,
    star: Star,
    star_half: StarHalf,

    // Communication
    thumbs_up: ThumbsUp,
    thumbs_down: ThumbsDown,
    message_circle: MessageCircle,
    mail: Mail,
    send: Send,
    share: Share,
    link: Link,
    external_link: ExternalLink,

    // Actions
    download: Download,
    upload: Upload,
    plus: Plus,
    minus: Minus,
    edit: Edit,
    trash: Trash,
    save: Save,
} as const satisfies Record<string, LucideIcon>;

interface IconPickerProps {
    value?: string;
    onChange: (icon: string) => void;
}

export function IconPicker({ value, onChange }: IconPickerProps) {
    const SelectedIcon = value ? ICON_MAP[value] : null;
    const [searchQuery, setSearchQuery] = useState('');

    const filteredIcons = Object.entries(ICON_MAP).filter(([name]) =>
        name.toLowerCase().includes(searchQuery.toLowerCase().replace(/ /g, '_')),
    );

    return (
        <Popover modal={true}>
            <PopoverTrigger asChild>
                <Tooltip>
                    <TooltipTrigger asChild>
                        <Button
                            variant='outline'
                            size='icon'
                            className={cn('h-9 w-9 shrink-0', !value && 'text-muted-foreground')}
                            aria-label='Choose icon'
                        >
                            {SelectedIcon ? <SelectedIcon className='h-4 w-4' /> : <HelpCircle className='h-4 w-4' />}
                        </Button>
                    </TooltipTrigger>
                    <TooltipContent side='bottom'>Choose icon</TooltipContent>
                </Tooltip>
            </PopoverTrigger>
            <PopoverContent className='w-80 p-0'>
                <div className='p-2 border-b'>
                    <Input
                        placeholder='Search icons...'
                        value={searchQuery}
                        onChange={e => setSearchQuery(e.target.value)}
                        className='h-8'
                    />
                </div>
                <ScrollArea className='h-64'>
                    <div className='grid grid-cols-6 gap-2 p-2'>
                        {filteredIcons.map(([name, Icon]) => (
                            <div
                                key={name}
                                className={cn(
                                    'h-10 w-10 rounded-md cursor-pointer border flex items-center justify-center transition-all hover:bg-accent hover:text-accent-foreground',
                                    value === name ? 'bg-accent text-accent-foreground ring-2 ring-primary' : '',
                                )}
                                onClick={() => onChange(name)}
                                title={name.replace(/_/g, ' ')}
                            >
                                <Icon className='h-5 w-5' />
                            </div>
                        ))}
                        {filteredIcons.length === 0 && (
                            <div className='col-span-6 text-center py-4 text-sm text-muted-foreground'>
                                No icons found
                            </div>
                        )}
                    </div>
                </ScrollArea>
            </PopoverContent>
        </Popover>
    );
}
